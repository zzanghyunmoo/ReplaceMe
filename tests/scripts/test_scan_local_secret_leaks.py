#!/usr/bin/env python3
"""Focused tests for the local secret leak scanner."""

from __future__ import annotations

import json
import os
from pathlib import Path
import stat
import subprocess
import sys
import tempfile
import threading
import unittest
from collections.abc import Mapping
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import cast
from urllib.parse import parse_qs, urlparse


REPO_ROOT = Path(__file__).resolve().parents[2]
SCRIPT = REPO_ROOT / "scripts" / "scan-local-secret-leaks.py"


class LogApi:
    def __init__(self, pages: Mapping[int, object]):
        self.pages = pages
        self.requests: list[dict[str, list[str]]] = []
        owner = self

        class Handler(BaseHTTPRequestHandler):
            def do_GET(self) -> None:  # noqa: N802 - stdlib handler contract
                parsed = urlparse(self.path)
                query = parse_qs(parsed.query)
                owner.requests.append(query)
                try:
                    page = int(query["page"][0])
                except (KeyError, ValueError, IndexError):
                    self.send_error(400)
                    return
                payload = owner.pages.get(page, [])
                body = json.dumps(payload).encode()
                self.send_response(200)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)

            def log_message(self, format: str, *args: object) -> None:
                del format, args

        self.server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)

    @property
    def base_url(self) -> str:
        host, port = cast(tuple[str, int], self.server.server_address)
        return f"http://{host}:{port}"

    def __enter__(self) -> "LogApi":
        self.thread.start()
        return self

    def __exit__(self, *_args: object) -> None:
        self.server.shutdown()
        self.server.server_close()
        self.thread.join()


class SecretLeakScannerTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.logs = self.root / "logs"
        self.logs.mkdir()
        (self.logs / "devautomation-test.log").write_text("ordinary file log\n")
        self.env_file = self.root / ".env"
        self.env_file.write_text("")
        self.tmpdir = self.root / "tmp"
        self.tmpdir.mkdir()
        self.bin_dir = self.root / "bin"
        self.bin_dir.mkdir()
        self._write_docker("#!/bin/sh\nprintf 'ordinary compose log\\n'\n")

    def tearDown(self) -> None:
        self.temp.cleanup()

    def _write_docker(self, body: str) -> None:
        docker = self.bin_dir / "docker"
        docker.write_text(body)
        docker.chmod(docker.stat().st_mode | stat.S_IXUSR)

    def _run(self, api: LogApi, page_size: int = 2) -> subprocess.CompletedProcess[str]:
        env = os.environ.copy()
        env["PATH"] = f"{self.bin_dir}{os.pathsep}{env.get('PATH', '')}"
        env["TMPDIR"] = str(self.tmpdir)
        return subprocess.run(
            [
                sys.executable,
                str(SCRIPT),
                "--base-url",
                api.base_url,
                "--ticket-id",
                "00000000-0000-0000-0000-000000000001",
                "--env-file",
                str(self.env_file),
                "--logs-dir",
                str(self.logs),
                "--page-size",
                str(page_size),
            ],
            cwd=REPO_ROOT,
            env=env,
            capture_output=True,
            text=True,
            check=False,
        )

    def test_paginates_until_short_page_and_scans_decoded_content(self) -> None:
        pages = {
            1: [{"content": "first"}, {"content": "second"}],
            2: [{"content": "last"}],
        }
        with LogApi(pages) as api:
            result = self._run(api)

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(["1", "2"], [request["page"][0] for request in api.requests])
        self.assertTrue(all(request["pageSize"] == ["2"] for request in api.requests))

    def test_detects_escaped_quote_backslash_and_newline_without_printing_values(
        self,
    ) -> None:
        quote_secret = 'quote-"-secret'
        backslash_secret = r"slash-\-secret"
        newline_secret = "line-one\nline-two"
        self.env_file.write_text(
            'DEVAUTOMATION_Agent__AnthropicApiKey="quote-\\"-secret"\n'
            'DEVAUTOMATION_Agent__GitHubToken="slash-\\\\-secret"\n'
            'DEVAUTOMATION_Agent__GitLabToken="line-one\\nline-two"\n'
        )
        pages = {
            1: [
                {"Content": quote_secret},
                {"content": backslash_secret},
            ],
            2: [{"content": newline_secret}],
        }
        with LogApi(pages) as api:
            result = self._run(api)

        output = result.stdout + result.stderr
        self.assertEqual(1, result.returncode, output)
        for key in ("AnthropicApiKey", "GitHubToken", "GitLabToken"):
            self.assertIn(key, output)
        for secret in (quote_secret, backslash_secret, newline_secret):
            self.assertNotIn(secret, output)

    def test_detects_common_token_patterns_in_raw_log_surface(self) -> None:
        tokens = {
            "GitHub token": "ghp_abcdefghijklmnopqrstuvwxyz1234567890",
            "GitLab token": "glpat-abcdefghijklmnopqrstuvwxyz123456",
            "Anthropic token": "sk-ant-abcdefghijklmnopqrstuvwxyz123456",
            "Slack token": "".join(
                ("xo", "xb-", "1234567890", "-abcdefghijklmnopqrstuvwxyz")
            ),
        }
        (self.logs / "devautomation-test.log").write_text(
            "provider emitted " + " ".join(tokens.values()) + "\n"
        )
        with LogApi({1: [{"content": "ordinary execution log"}]}) as api:
            result = self._run(api)

        output = result.stdout + result.stderr
        self.assertEqual(1, result.returncode, output)
        for label, token in tokens.items():
            self.assertIn(label, output)
            self.assertNotIn(token, output)

    def test_temporary_storage_permissions_are_restrictive(self) -> None:
        self._write_docker(
            "#!/usr/bin/env python3\n"
            "import os\n"
            "from pathlib import Path\n"
            "import stat\n"
            "root = Path(os.environ['TMPDIR'])\n"
            "workspaces = list(root.glob('replaceme-secret-scan-*'))\n"
            "if len(workspaces) != 1 or stat.S_IMODE(workspaces[0].stat().st_mode) != 0o700:\n"
            "    raise SystemExit(10)\n"
            "files = list(workspaces[0].iterdir())\n"
            "if len(files) != 1 or stat.S_IMODE(files[0].stat().st_mode) != 0o600:\n"
            "    raise SystemExit(11)\n"
            "print('ordinary compose log')\n"
        )
        with LogApi({1: [{"content": "ordinary execution log"}]}) as api:
            result = self._run(api)

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual([], list(self.tmpdir.iterdir()))

    def test_validation_failure_is_nonzero_and_temporary_storage_is_cleaned(
        self,
    ) -> None:
        self._write_docker("#!/bin/sh\nprintf 'compose failed' >&2\nexit 7\n")
        with LogApi({1: [{"content": "ordinary execution log"}]}) as api:
            result = self._run(api)

        self.assertNotEqual(0, result.returncode)
        self.assertIn("compose log collection failed", result.stderr)
        self.assertEqual([], list(self.tmpdir.iterdir()))

    def test_invalid_execution_log_json_fails_closed_and_cleans_up(self) -> None:
        with LogApi({1: {"content": "not an array"}}) as api:
            result = self._run(api)

        self.assertNotEqual(0, result.returncode)
        self.assertIn("JSON array", result.stderr)
        self.assertEqual([], list(self.tmpdir.iterdir()))


if __name__ == "__main__":
    unittest.main()
