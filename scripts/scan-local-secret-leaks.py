#!/usr/bin/env python3
"""Fail-closed scan of local execution, file, and Compose logs for secrets."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
from urllib.error import HTTPError, URLError
from urllib.parse import quote, urlencode
from urllib.request import Request, urlopen


SECRET_KEYS = (
    "DEVAUTOMATION_Agent__AnthropicApiKey",
    "DEVAUTOMATION_Agent__GitHubToken",
    "DEVAUTOMATION_Agent__GitLabToken",
    "DEVAUTOMATION_Slack__BotToken",
    "DEVAUTOMATION_Slack__SigningSecret",
    "DEVAUTOMATION_Gmail__AccessToken",
    "DEVAUTOMATION_Jira__ApiToken",
    "DEVAUTOMATION_Linear__ApiKey",
    "DEVAUTOMATION_Notion__ApiToken",
    "DEVAUTOMATION_Confluence__ApiToken",
    "DEVAUTOMATION_Langfuse__PublicKey",
    "DEVAUTOMATION_Langfuse__SecretKey",
    "DEVAUTOMATION_LiteLLM__ApiKey",
    "DEVAUTOMATION_LiteLLM__VirtualKey",
    "DEVAUTOMATION_Telemetry__OtlpHeaders",
)

TOKEN_PATTERNS = (
    (
        "GitHub token",
        re.compile(r"(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,})"),
    ),
    ("GitLab token", re.compile(r"glpat-[A-Za-z0-9_-]{20,}")),
    ("Anthropic token", re.compile(r"sk-ant-[A-Za-z0-9_-]{10,}")),
    (
        "Slack token",
        re.compile(r"(?:xox[baprs]-[A-Za-z0-9-]{10,}|xapp-[A-Za-z0-9-]{10,})"),
    ),
)


class ScanError(Exception):
    """A safe-to-report validation or collection failure."""


def secure_write(path: Path, data: bytes) -> None:
    descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            descriptor = -1
            stream.write(data)
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    path.chmod(0o600)


def decode_double_quoted(value: str, line_number: int) -> str:
    decoded: list[str] = []
    escapes = {"n": "\n", "r": "\r", "t": "\t", "\\": "\\", '"': '"'}
    index = 1
    while index < len(value):
        character = value[index]
        if character == '"':
            trailing = value[index + 1 :].strip()
            if trailing and not trailing.startswith("#"):
                raise ScanError(f"invalid trailing content in .env line {line_number}")
            return "".join(decoded)
        if character == "\\":
            index += 1
            if index >= len(value):
                raise ScanError(f"unterminated escape in .env line {line_number}")
            escaped = value[index]
            decoded.append(escapes.get(escaped, f"\\{escaped}"))
        else:
            decoded.append(character)
        index += 1
    raise ScanError(f"unterminated double-quoted value in .env line {line_number}")


def decode_env_value(raw_value: str, line_number: int) -> str:
    value = raw_value.strip()
    if not value:
        return ""
    if value.startswith('"'):
        return decode_double_quoted(value, line_number)
    if value.startswith("'"):
        end = value.find("'", 1)
        if end < 0:
            raise ScanError(
                f"unterminated single-quoted value in .env line {line_number}"
            )
        trailing = value[end + 1 :].strip()
        if trailing and not trailing.startswith("#"):
            raise ScanError(f"invalid trailing content in .env line {line_number}")
        return value[1:end]
    return re.split(r"\s+#", value, maxsplit=1)[0].rstrip()


def load_secrets(env_file: Path) -> dict[str, str]:
    try:
        lines = env_file.read_text(encoding="utf-8").splitlines()
    except (OSError, UnicodeError) as error:
        raise ScanError(f"cannot read env file: {env_file}") from error

    configured: dict[str, str] = {}
    secret_keys = set(SECRET_KEYS)
    for line_number, line in enumerate(lines, start=1):
        stripped = line.strip()
        if not stripped or stripped.startswith("#") or "=" not in line:
            continue
        key, raw_value = line.split("=", 1)
        key = key.strip()
        if key.startswith("export "):
            key = key.removeprefix("export ").strip()
        if key not in secret_keys:
            continue
        value = decode_env_value(raw_value, line_number)
        if len(value) >= 8:
            configured[key] = value
    return configured


def fetch_execution_logs(
    base_url: str,
    ticket_id: str,
    page_size: int,
    raw_output: Path,
) -> list[str]:
    contents: list[str] = []
    raw_pages: list[bytes] = []
    page = 1
    while True:
        query = urlencode({"page": page, "pageSize": page_size})
        url = f"{base_url.rstrip('/')}/api/tickets/{quote(ticket_id, safe='')}/logs?{query}"
        try:
            with urlopen(Request(url, method="GET"), timeout=15) as response:
                body = response.read()
        except (HTTPError, URLError, TimeoutError, OSError) as error:
            raise ScanError(f"execution log request failed on page {page}") from error
        raw_pages.append(body)
        try:
            payload = json.loads(body)
        except (json.JSONDecodeError, UnicodeDecodeError) as error:
            raise ScanError(f"execution log page {page} is not valid JSON") from error
        if not isinstance(payload, list):
            raise ScanError(f"execution log page {page} must be a JSON array")
        for item_number, item in enumerate(payload, start=1):
            if not isinstance(item, dict):
                raise ScanError(
                    f"execution log page {page} item {item_number} must be an object"
                )
            content = item.get("content", item.get("Content"))
            if not isinstance(content, str):
                raise ScanError(
                    f"execution log page {page} item {item_number} has no string Content"
                )
            contents.append(content)
        if len(payload) < page_size:
            break
        page += 1

    secure_write(raw_output, b"\n".join(raw_pages))
    if not contents:
        raise ScanError("execution logs must contain at least one entry")
    return contents


def collect_compose_logs(raw_output: Path) -> str:
    try:
        result = subprocess.run(
            ["docker", "compose", "logs", "--no-color", "api", "worker", "migrate"],
            capture_output=True,
            check=False,
        )
    except OSError as error:
        raise ScanError("compose log collection failed to start") from error
    if result.returncode != 0:
        raise ScanError(
            f"compose log collection failed with exit code {result.returncode}"
        )
    if not result.stdout:
        raise ScanError("compose log collection returned no output")
    secure_write(raw_output, result.stdout)
    return result.stdout.decode("utf-8", errors="replace")


def collect_file_logs(logs_dir: Path) -> str:
    if not logs_dir.is_dir():
        raise ScanError(f"file log directory does not exist: {logs_dir}")
    chunks: list[str] = []
    for path in sorted(logs_dir.glob("devautomation-*.log")):
        try:
            if path.is_file() and path.stat().st_size > 0:
                chunks.append(path.read_text(encoding="utf-8", errors="replace"))
        except OSError as error:
            raise ScanError(f"cannot read file log: {path}") from error
    if not chunks:
        raise ScanError("at least one non-empty devautomation file log is required")
    return "\n".join(chunks)


def scan(log_text: str, secrets: dict[str, str]) -> tuple[list[str], list[str]]:
    leaked_keys = [key for key, value in secrets.items() if value in log_text]
    matched_patterns = [
        label for label, pattern in TOKEN_PATTERNS if pattern.search(log_text)
    ]
    return leaked_keys, matched_patterns


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--base-url",
        default=os.environ.get("BASE_URL"),
        help="DevAutomation API base URL",
    )
    parser.add_argument(
        "--ticket-id",
        required=True,
        help="Ticket whose complete execution log is scanned",
    )
    parser.add_argument(
        "--env-file",
        type=Path,
        default=Path(".env"),
        help="Configured local environment file",
    )
    parser.add_argument(
        "--logs-dir",
        type=Path,
        default=Path("logs"),
        help="API/worker file log directory",
    )
    parser.add_argument("--page-size", type=int, default=500, help=argparse.SUPPRESS)
    args = parser.parse_args()
    if not args.base_url:
        parser.error("--base-url or BASE_URL is required")
    if not 1 <= args.page_size <= 500:
        parser.error("--page-size must be between 1 and 500")
    return args


def main() -> int:
    args = parse_args()
    try:
        with tempfile.TemporaryDirectory(prefix="replaceme-secret-scan-") as temporary:
            workspace = Path(temporary)
            workspace.chmod(0o700)
            execution_contents = fetch_execution_logs(
                args.base_url,
                args.ticket_id,
                args.page_size,
                workspace / "execution-logs.json",
            )
            compose_text = collect_compose_logs(workspace / "compose-logs.txt")
            file_text = collect_file_logs(args.logs_dir)
            secrets = load_secrets(args.env_file)
            combined = "\n".join((*execution_contents, compose_text, file_text))
            leaked_keys, matched_patterns = scan(combined, secrets)
            if leaked_keys or matched_patterns:
                if leaked_keys:
                    print(
                        "SECRET LEAK configured keys: "
                        + ", ".join(sorted(leaked_keys)),
                        file=sys.stderr,
                    )
                if matched_patterns:
                    print(
                        "SECRET-LIKE PATTERNS: " + ", ".join(sorted(matched_patterns)),
                        file=sys.stderr,
                    )
                return 1
    except ScanError as error:
        print(f"secret scan validation failed: {error}", file=sys.stderr)
        return 2

    print("no configured secret values or common token patterns found in local logs")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
