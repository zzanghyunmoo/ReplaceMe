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
DEVAUTOMATION_Notifier__Provider=None
DEVAUTOMATION_ProfileReadiness__SelectedProfile=
```

API를 재시작합니다.

```bash
docker compose down
docker compose up --build api postgres kafka
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
- Anthropic/GitHub/GitLab/Slack/Jira/Linear secret 원문이 보이면 실패입니다.
- secret은 `[REDACTED]`로 보여야 합니다.

추가 secret scan은 실제 `.env`에 설정된 secret 값을 기준으로 수행합니다. 아래 스크립트는
secret 원문을 출력하지 않고, 어떤 환경변수 이름이 log에서 발견됐는지만 표시합니다.

```bash
curl -s "$BASE_URL/api/tickets/$AGENT_OK_TICKET_ID/logs?page=1&pageSize=500" \
  > /tmp/replaceme-agent-logs.json

python3 - <<'PY'
from pathlib import Path

log_text = Path('/tmp/replaceme-agent-logs.json').read_text(errors='ignore')
log_text += '\n' + '\n'.join(
    p.read_text(errors='ignore') for p in Path('logs').glob('devautomation-*.log')
)
secret_keys = [
    'DEVAUTOMATION_Agent__AnthropicApiKey',
    'DEVAUTOMATION_Agent__GitHubToken',
    'DEVAUTOMATION_Agent__GitLabToken',
    'DEVAUTOMATION_Slack__BotToken',
    'DEVAUTOMATION_Slack__SigningSecret',
    'DEVAUTOMATION_Linear__ApiKey',
    'DEVAUTOMATION_Notion__ApiToken',
    'DEVAUTOMATION_Jira__ApiToken',
    'DEVAUTOMATION_Confluence__ApiToken',
]
values = {}
for line in Path('.env').read_text(errors='ignore').splitlines():
    if not line or line.lstrip().startswith('#') or '=' not in line:
        continue
    key, value = line.split('=', 1)
    if key in secret_keys and len(value.strip()) >= 8:
        values[key] = value.strip()
leaked = [key for key, value in values.items() if value in log_text]
if leaked:
    print('SECRET LEAK keys:', ', '.join(leaked))
    raise SystemExit(1)
print('no configured secret values found in logs')
PY
```

패턴 기반 보조 검사도 함께 실행합니다.

```bash
grep -R -E "(ghp_|github_pat_|glpat-|sk-ant-|xox[baprs]-|xapp-)" logs \
  && echo "SECRET-LIKE PATTERN FOUND" \
  || echo "no common secret-like pattern in file logs"
```

## AGENT-007. readiness gate safety net 확인

현재 `KafkaAgentWorker`는 API process 안의 hosted service로 실행됩니다. 그래서
Docker Compose 기준으로 API는 켜 둔 채 worker만 안정적으로 멈추는 절차가 아직 없습니다.
이 케이스는 **재현성 낮은 탐색 테스트**로 취급하고, 자동 QA pass/fail 기준으로 삼지
않습니다.

탐색 절차:

1. pre-run gate를 끈 상태에서 ticket을 만듭니다.
2. worker가 실행하기 전에 API/worker를 멈춥니다. 이 단계는 race-prone입니다.
3. `.env`에서 `DEVAUTOMATION_ProfileReadiness__SelectedProfile=personal-github-linear-notion`을 켜고 required check를 실패시킵니다.
4. API/worker를 다시 시작합니다.

관측 포인트:

- worker가 `AgentJob.RunAsync` 시작 시 readiness를 다시 평가합니다.
- runnable이 아니면 ticket을 `Failed`로 만들고 `failReason`은 `Readiness gate blocked:`로 시작합니다.
- Docker agent container는 생성되지 않습니다.

결정적 QA가 필요하면 후속 작업으로 다음 중 하나를 먼저 추가합니다.

- worker만 끄는 설정(`DEVAUTOMATION_Agent__WorkerEnabled=false` 등)
- API service와 worker service의 compose 분리
- queued ticket/ Kafka message를 seed하는 테스트 harness

## 완료 체크리스트

- [ ] agent image build/inspect 성공
- [ ] invalid repo smoke test가 `Failed`로 기록됨
- [ ] 실패 ticket logs 조회 가능
- [ ] 종료 후 agent container가 남지 않음
- [ ] scratch repo full run이 branch/PR을 생성
- [ ] 성공 ticket에 `prUrl` 저장
- [ ] execution log에 secret 원문 노출 없음
- [ ] readiness safety net은 탐색 테스트로만 수행하고, 결정적 검증에는 별도 worker/harness가 필요함을 기록

<!-- markdownlint-enable MD013 -->
