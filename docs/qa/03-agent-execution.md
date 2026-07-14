# QA 03. Kafka와 Docker 에이전트 실행

<!-- markdownlint-disable MD013 -->

## 목적

티켓 생성 후 Kafka worker가 job을 consume하고, Docker container에서 coding agent를
실행하며, 결과를 ticket 상태/로그/PR URL로 남기는지 확인합니다.

## 사전 준비

먼저 다음 QA를 통과합니다.

1. [`00-local-runbook.md`](./00-local-runbook.md)
2. [`02-ticket-management.md`](./02-ticket-management.md)

기본 변수:

```bash
export BASE_URL=http://localhost:8080
```

## 테스트 모드

| 모드 | 목적 | 필요한 credential | 기대 terminal 상태 |
| --- | --- | --- | --- |
| 실패 경로 smoke | worker와 failure 기록 확인 | 없음 또는 최소 | `Failed` |
| 성공 경로 full run | branch/push/PR 생성 확인 | Anthropic + GitHub/GitLab write token | `Completed` |

처음에는 실패 경로 smoke test로 worker, Docker, log persistence를 확인하고, 이후
scratch repository로 성공 경로를 확인합니다.

## AGENT-001. agent image가 준비되어 있다

```bash
docker compose --profile build-only build agent-image
docker image inspect devautomation-claude:latest >/dev/null && echo "agent image ready"
```

기대 결과:

- image inspect가 성공합니다.
- readiness profile을 켰다면 `agent.image.available` check도 통과해야 합니다.

## AGENT-002. 실패 경로 smoke test

외부 credential이 없거나 실제 PR을 만들고 싶지 않을 때 사용하는 안전한 테스트입니다.
invalid repository를 넣어 worker가 실패를 기록하는지 확인합니다.

```bash
curl -s -X POST "$BASE_URL/api/tickets" \
  -H 'Content-Type: application/json' \
  -d '{
    "title": "QA agent failure path",
    "description": "This intentionally uses an invalid repository to verify failure handling.",
    "repoUrl": "https://github.com/example/not-a-real-repo-for-replaceme-qa.git",
    "baseBranch": "main"
  }' | tee /tmp/replaceme-agent-failure.json | jq .

export AGENT_FAIL_TICKET_ID=$(jq -r '.id' /tmp/replaceme-agent-failure.json)
```

상태가 terminal이 될 때까지 조회합니다.

```bash
watch -n 2 "curl -s $BASE_URL/api/tickets/$AGENT_FAIL_TICKET_ID | jq '{id,status,failReason,containerId}'"
```

기대 결과:

- `Pending`에서 `Running`으로 이동합니다.
- repository clone 실패 또는 agent 실행 실패로 `Failed`가 됩니다.
- `failReason`에 실패 원인이 남습니다.
- `GET /logs`에서 container output 일부를 확인할 수 있습니다.

```bash
curl -s "$BASE_URL/api/tickets/$AGENT_FAIL_TICKET_ID/logs?page=1&pageSize=100" | jq .
```

## AGENT-003. container 정리 확인

agent run이 끝난 뒤 container가 제거되는지 확인합니다.

```bash
docker ps -a \
  --filter "label=devautomation.ticket-id=$AGENT_FAIL_TICKET_ID" \
  --format '{{.ID}} {{.Status}} {{.Names}}'
```

기대 결과:

- 정상적으로 제거되면 출력이 없습니다.
- 남아 있다면 `docker logs <container-id>`로 원인을 확인하고 수동 삭제합니다.

## AGENT-004. 성공 경로 full run 준비

실제 PR을 만들 QA용 scratch repository를 준비합니다. 개인/회사 중요 repo 대신 작은
테스트 repo를 사용합니다.

실행 전에 sandbox 정보를 명시적으로 기록합니다.

| 대상 | QA 값 | cleanup |
| --- | --- | --- |
| GitHub scratch repo | `https://github.com/<owner>/<scratch-repo>.git` | PR close, `agent/ticket-*` branch 삭제 |
| Base branch | `main` 또는 QA branch | 중요 branch가 아닌지 확인 |
| Anthropic/GitHub token | QA 권한 token | 테스트 후 필요 시 revoke |

`.env` 예시:

```env
DEVAUTOMATION_Agent__RemoteRepositoryProvider=GitHub
DEVAUTOMATION_Agent__GitHubToken=<scratch repo push 가능한 token>
DEVAUTOMATION_Agent__AnthropicApiKey=<Anthropic API key>
DEVAUTOMATION_CodingAgent__Provider=ClaudeCode
DEVAUTOMATION_CodingAgent__ClaudeCommand=claude
DEVAUTOMATION_Agent__ExecutionIsolationProfile=LocalDevelopment
DEVAUTOMATION_Agent__DockerSocketMode=LocalDockerSocket
DEVAUTOMATION_Agent__AllowLocalDockerSocket=true
DEVAUTOMATION_Agent__AllowLocalDockerSocketInProductionLike=false
DEVAUTOMATION_Notifier__Provider=None
# Agent container Approval MCP는 provider 선택을 전달받지 않으므로 Slack 값도 비웁니다.
DEVAUTOMATION_Slack__BotToken=
DEVAUTOMATION_Slack__SigningSecret=
DEVAUTOMATION_Slack__ChannelId=
DEVAUTOMATION_ProfileReadiness__SelectedProfile=
```

Broker evidence를 보존한 채 API와 worker만 build/recreate합니다.

```bash
docker compose up -d --build --force-recreate api worker
```

## AGENT-005. 성공 경로 full run

```bash
export QA_REPO_URL=https://github.com/<owner>/<scratch-repo>.git

curl -s -X POST "$BASE_URL/api/tickets" \
  -H 'Content-Type: application/json' \
  -d "{
    \"title\": \"QA add harmless note\",
    \"description\": \"Add a small QA-NOTES.md file with one sentence, then run the existing test or explain if no tests exist.\",
    \"repoUrl\": \"$QA_REPO_URL\",
    \"baseBranch\": \"main\"
  }" | tee /tmp/replaceme-agent-success.json | jq .

export AGENT_OK_TICKET_ID=$(jq -r '.id' /tmp/replaceme-agent-success.json)
```

상태를 모니터링합니다.

```bash
watch -n 5 "curl -s $BASE_URL/api/tickets/$AGENT_OK_TICKET_ID | jq '{status,prUrl,failReason}'"
```

기대 결과:

- ticket이 `Running`이 됩니다.
- agent가 scratch repo에 `agent/ticket-<id>` branch를 push합니다.
- 변경 사항이 있으면 PR을 만들고 `prUrl`을 저장합니다.
- 최종 status는 `Completed`입니다.

## AGENT-006. 실행 로그와 secret redaction 확인

```bash
curl -s "$BASE_URL/api/tickets/$AGENT_OK_TICKET_ID/logs?page=1&pageSize=200" | jq .
```

기대 결과:

- agent stdout/stderr가 execution log로 저장됩니다.
- Anthropic/GitHub/GitLab/Slack/Jira/Linear/Gmail/Notion/Confluence/Langfuse/LiteLLM
  secret 원문이 보이면 실패입니다.
- secret은 `[REDACTED]`로 보여야 합니다.
- redaction은 agent stream 중심이므로 API/worker file log와 Compose stdout도 함께
  검사합니다.

추가 secret scan은 실제 `.env`에 설정된 secret 값을 기준으로 수행합니다. 아래 스크립트는
secret 원문을 출력하지 않고, 어떤 환경변수 이름이 log에서 발견됐는지만 표시합니다.

```bash
set -euo pipefail
curl -fsS "$BASE_URL/api/tickets/$AGENT_OK_TICKET_ID/logs?page=1&pageSize=500" \
  > /tmp/replaceme-agent-logs.json
docker compose logs --no-color api worker migrate \
  > /tmp/replaceme-compose-logs.txt
test -s /tmp/replaceme-agent-logs.json
test -s /tmp/replaceme-compose-logs.txt
test -d logs
python3 - <<'PY'
from pathlib import Path
import json

payload = json.loads(Path('/tmp/replaceme-agent-logs.json').read_text())
if not isinstance(payload, list) or not payload:
    raise SystemExit('execution log JSON must be a non-empty array')
if not any(p.stat().st_size > 0 for p in Path('logs').glob('devautomation-*.log')):
    raise SystemExit('at least one non-empty file log is required')
PY

python3 - <<'PY'
from pathlib import Path
import shlex

log_text = Path('/tmp/replaceme-agent-logs.json').read_text(errors='ignore')
log_text += '\n' + Path('/tmp/replaceme-compose-logs.txt').read_text(errors='ignore')
log_text += '\n' + '\n'.join(
    p.read_text(errors='ignore') for p in Path('logs').glob('devautomation-*.log')
)
secret_keys = [
    'DEVAUTOMATION_Agent__AnthropicApiKey',
    'DEVAUTOMATION_Agent__GitHubToken',
    'DEVAUTOMATION_Agent__GitLabToken',
    'DEVAUTOMATION_Slack__BotToken',
    'DEVAUTOMATION_Slack__SigningSecret',
    'DEVAUTOMATION_Gmail__AccessToken',
    'DEVAUTOMATION_Linear__ApiKey',
    'DEVAUTOMATION_Notion__ApiToken',
    'DEVAUTOMATION_Jira__ApiToken',
    'DEVAUTOMATION_Confluence__ApiToken',
    'DEVAUTOMATION_Langfuse__PublicKey',
    'DEVAUTOMATION_Langfuse__SecretKey',
    'DEVAUTOMATION_LiteLLM__ApiKey',
    'DEVAUTOMATION_LiteLLM__VirtualKey',
    'DEVAUTOMATION_Telemetry__OtlpHeaders',
]
values = {}
for line in Path('.env').read_text(errors='ignore').splitlines():
    if not line or line.lstrip().startswith('#') or '=' not in line:
        continue
    key, raw_value = line.split('=', 1)
    try:
        value = ' '.join(shlex.split(raw_value, comments=True, posix=True))
    except ValueError:
        value = raw_value.strip()
    if key in secret_keys and len(value) >= 8:
        values[key] = value
leaked = [key for key, value in values.items() if value in log_text]
if leaked:
    print('SECRET LEAK keys:', ', '.join(leaked))
    raise SystemExit(1)
print('no configured secret values found in logs')
PY
```

패턴 기반 보조 검사도 함께 실행합니다.

```bash
set -euo pipefail
test -d logs
test -s /tmp/replaceme-agent-logs.json
test -s /tmp/replaceme-compose-logs.txt
python3 - <<'PY'
from pathlib import Path
import json

payload = json.loads(Path('/tmp/replaceme-agent-logs.json').read_text())
if not isinstance(payload, list) or not payload:
    raise SystemExit('execution log JSON must be a non-empty array')
if not any(p.stat().st_size > 0 for p in Path('logs').glob('devautomation-*.log')):
    raise SystemExit('at least one non-empty file log is required')
PY
set +e
grep -R -q -E "(ghp_|github_pat_|glpat-|sk-ant-|xox[baprs]-|xapp-)" \
  logs /tmp/replaceme-agent-logs.json /tmp/replaceme-compose-logs.txt
grep_status=$?
set -e
case "$grep_status" in
  0) echo "SECRET-LIKE PATTERN FOUND"; exit 1 ;;
  1) echo "no common secret-like pattern in execution/file/Compose logs" ;;
  *) echo "secret pattern scan failed"; exit "$grep_status" ;;
esac
rm -f /tmp/replaceme-agent-logs.json /tmp/replaceme-compose-logs.txt
```

## AGENT-007. agent container secret allowlist spot check

agent run 중 container environment를 확인할 수 있는 짧은 실행 창이 있으면, 선택하지 않은
provider/gateway secret이 없는지 확인합니다.

```bash
docker inspect \
  --format '{{range .Config.Env}}{{println .}}{{end}}' \
  "<agent-container-id>" \
  | grep -E 'GITHUB_TOKEN|GITLAB_TOKEN|ANTHROPIC_API_KEY|LANGFUSE|LITELLM'
```

기대 결과:

- GitHub provider run이면 `GITHUB_TOKEN`은 있을 수 있지만 `GITLAB_TOKEN`은 없어야 합니다.
- GitLab provider run이면 `GITLAB_TOKEN`은 있을 수 있지만 `GITHUB_TOKEN`은 없어야 합니다.
- 현재 direct Claude Code path는 `ANTHROPIC_API_KEY`만 사용합니다.
- Langfuse/LiteLLM secret은 redaction 대상이지만 기본 agent container env에는 없어야 합니다.
- Slack 외 notifier를 선택한 Approval MCP full E2E는 현재 별도 검증이 필요합니다.
  agent container의 notifier 환경 전달이 선택한 provider를 완전히 보존한다고 가정하지
  않습니다.

## AGENT-008. readiness gate safety net 확인

`DevAutomation.Worker`가 API와 분리되어 있으므로 queued ticket을 만든 뒤 worker만
나중에 시작하는 방식으로 `AgentJob.RunAsync`의 readiness safety net을 비교적
결정적으로 확인할 수 있습니다.

절차:

1. `.env`에서 `DEVAUTOMATION_ProfileReadiness__SelectedProfile=`처럼 pre-run gate를 끕니다.
2. Broker state를 보존하면서 worker만 멈추고 API/DB/Kafka를 유지합니다.

   ```bash
   docker compose stop worker || true
   docker compose up -d --build api postgres kafka
   ```

3. ticket을 생성해 Kafka에 job을 enqueue합니다. worker가 꺼져 있으므로 ticket은 보통 `Pending`에 머뭅니다.
4. `.env`에서 `DEVAUTOMATION_ProfileReadiness__SelectedProfile=personal-github-linear-notion`을 켜고 required check가 실패하도록 credential이나 agent image 조건을 의도적으로 비웁니다.
5. worker만 시작합니다.

   ```bash
   docker compose up -d --build worker
   ```

관측 포인트:

- worker가 `AgentJob.RunAsync` 시작 시 readiness를 다시 평가합니다.
- runnable이 아니면 ticket을 `Failed`로 만들고 `failReason`은 `Readiness gate blocked:`로 시작합니다.
- Docker agent container는 생성되지 않습니다.

이 케이스는 readiness profile의 실제 required check 구성에 의존합니다. 로컬 환경이 이미
runnable이면 의도적으로 credential을 비운 별도 `.env`나 테스트 profile을 사용합니다.

## AGENT-009. Kafka retry/DLQ 설정과 poison message 확인

worker는 agent job message 본문에 `Attempt`, `LastFailureReason`, `LastFailedAt`를 저장합니다.
`Queue:MaxAttempts` 이상 실패한 worker 처리 예외 또는 parsing할 수 없는 poison message는
`Queue:KafkaDlqTopic`(기본 `devautomation.agent-jobs.dlq`)으로 이동합니다.

가벼운 설정 검증:

```bash
docker compose config --quiet
```

Redpanda가 떠 있는 환경에서 고유 marker를 포함한 invalid payload를 publish합니다.
Worker log에서 해당 message의 DLQ publish를 확인한 뒤 DLQ의 최신 record와 marker를
대조합니다.

```bash
export DLQ_MARKER="qa-poison-$(date +%s)"
printf 'not-json-%s\n' "$DLQ_MARKER" \
  | docker compose exec -T kafka \
      rpk topic produce devautomation.agent-jobs

docker compose logs --no-color --since=1m worker \
  | grep 'invalid Kafka queue message'

docker compose exec -T kafka \
  rpk topic consume devautomation.agent-jobs.dlq \
  -o -1 -n 1 --format '%v\n' \
  | tee /tmp/replaceme-dlq-latest.json

grep -F "$DLQ_MARKER" /tmp/replaceme-dlq-latest.json >/dev/null
```

기대 결과:

- worker log에 invalid Kafka queue message가 DLQ로 publish됐다는 warning이 남습니다.
- 최신 DLQ payload의 sanitized original value에 `$DLQ_MARKER`가 있습니다.
- DLQ payload에는 source topic/partition/offset과 failure reason도 있습니다.
- DLQ publish가 실패하면 worker는 원본 offset을 commit하지 않고 reconnect loop에서 다시 시도합니다.

## 완료 체크리스트

- [ ] agent image build/inspect 성공
- [ ] invalid repo smoke test가 `Failed`로 기록됨
- [ ] 실패 ticket logs 조회 가능
- [ ] 종료 후 agent container가 남지 않음
- [ ] scratch repo full run이 branch/PR을 생성
- [ ] 성공 ticket에 `prUrl` 저장
- [ ] execution log에 Langfuse/LiteLLM 포함 secret 원문 노출 없음
- [ ] agent container env에 선택하지 않은 provider/gateway secret이 없음
- [ ] worker를 나중에 시작하는 방식으로 readiness safety net이 agent container 생성 전에 차단함을 확인
- [ ] poison message가 `devautomation.agent-jobs.dlq`에 sanitized context와 함께 들어감

<!-- markdownlint-enable MD013 -->
