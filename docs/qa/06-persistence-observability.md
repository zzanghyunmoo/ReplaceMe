# QA 06. 저장소와 관측성

<!-- markdownlint-disable MD013 -->

## 목적

티켓, 승인 요청, 실행 로그가 PostgreSQL에 저장되고, API/worker 로그가 파일에 남으며,
민감값 redaction이 동작하는지 확인합니다.

## 사전 준비

먼저 다음 QA를 통과합니다.

1. [`00-local-runbook.md`](./00-local-runbook.md)
2. [`02-ticket-management.md`](./02-ticket-management.md)
3. 필요 시 [`03-agent-execution.md`](./03-agent-execution.md)

기본 변수:

```bash
export BASE_URL=http://localhost:8080
```

## OBS-001. DB table 확인

```bash
docker compose exec postgres \
  psql -U devautomation -d devautomation -c '\dt'
```

기대 결과:

- `tickets`
- `approval_requests`
- `execution_logs`
- EF Core migration history table

## OBS-002. 티켓 row 저장 확인

먼저 ticket을 하나 만듭니다.

```bash
curl -s -X POST "$BASE_URL/api/tickets" \
  -H 'Content-Type: application/json' \
  -d '{
    "title": "QA persistence ticket",
    "description": "Verify ticket row persistence.",
    "repoUrl": "https://github.com/example/repo.git",
    "baseBranch": "main"
  }' | tee /tmp/replaceme-observe-ticket.json | jq .

export OBS_TICKET_ID=$(jq -r '.id' /tmp/replaceme-observe-ticket.json)
```

DB에서 확인합니다.

```bash
docker compose exec postgres psql -U devautomation -d devautomation \
  -c "select \"Id\", \"Title\", \"Status\", \"FailReason\" from tickets where \"Id\" = '$OBS_TICKET_ID';"
```

기대 결과:

- 생성한 ticket id가 조회됩니다.
- API 응답의 status와 DB status가 일치합니다.

## OBS-003. 실행 로그 row 확인

worker가 ticket을 처리한 뒤 확인합니다.

```bash
docker compose exec postgres psql -U devautomation -d devautomation \
  -c "select \"Timestamp\", \"EventType\", left(\"Content\", 120) from execution_logs where \"TicketId\" = '$OBS_TICKET_ID' order by \"Timestamp\" limit 20;"
```

API로도 같은 데이터를 확인합니다.

```bash
curl -s "$BASE_URL/api/tickets/$OBS_TICKET_ID/logs?page=1&pageSize=20" | jq .
```

기대 결과:

- agent container가 실행되었다면 execution log row가 생깁니다.
- API log endpoint와 DB row가 같은 흐름을 보여줍니다.

## OBS-004. approval row 확인

approval flow QA에서 pending approval을 만든 뒤 확인합니다.

```bash
docker compose exec postgres psql -U devautomation -d devautomation \
  -c 'select "Id", "TicketId", "ToolName", "Status", "RespondedAt", "ResponseReason" from approval_requests order by "RequestedAt" desc limit 20;'
```

기대 결과:

- pending approval은 `Status=Pending`입니다.
- approve/reject 후 `Approved` 또는 `Rejected`가 되고 `RespondedAt`이 채워집니다.
- timeout 후에는 `TimedOut`입니다.

## OBS-005. 파일 로그 확인

```bash
ls -la logs
find logs -type f -maxdepth 1 -name 'devautomation-*.log' -print
```

최근 로그를 확인합니다.

```bash
tail -n 100 logs/devautomation-*.log
```

기대 결과:

- API request log가 남습니다.
- worker/agent 오류가 있으면 stack trace 또는 warning이 남습니다.
- 로그 파일이 없으면 API/worker container volume mount `./logs:/app/logs`를 확인합니다.

## OBS-006. secret redaction 확인

실제 secret 원문이 execution log나 file log에 남지 않는지 확인합니다. pattern 검색만으로는
provider별 secret을 놓칠 수 있으므로, `.env`에 설정된 실제 값도 원문을 출력하지 않고
대조합니다. `OBS_TICKET_ID`는 execution log가 하나 이상 있는 처리 완료 Ticket이어야
합니다.

```bash
python3 scripts/scan-local-secret-leaks.py \
  --base-url "$BASE_URL" \
  --ticket-id "$OBS_TICKET_ID"
```

공통 스크립트는 execution log endpoint를 마지막 page까지 조회하고 JSON의 decoded
`Content`를 API/worker file log 및 Compose stdout과 합쳐 검사합니다. 임시 저장소는 예측
불가능한 경로에 `0700`으로 만들고 파일은 `0600`으로 제한하며, 성공·실패와 관계없이
정리합니다. configured secret 값, common GitHub/GitLab/Anthropic/Slack token pattern,
입력 및 local log surface 검증 중 하나라도 실패하면 nonzero로 종료합니다.

기대 결과:

- configured secret value가 발견되면 실패입니다. 출력은 key 이름만 표시하고 secret 원문은 표시하지 않습니다.
- common token pattern이 보이면 실패합니다. 출력에는 pattern 종류만 표시합니다.
- secret이 필요한 경우 `[REDACTED]`로 보여야 합니다.
- secret catalog coverage는 readiness profile의 `secrets.redaction.coverage` check로 함께 확인합니다.
- Agent stream redaction이 Serilog/provider/Compose stdout 전체를 보장하지 않으므로 모든
  local log surface를 검사합니다. 외부 관측 sink나 파생·인코딩된 secret의 부재까지
  증명하는 검사는 아닙니다.

## OBS-007. OpenTelemetry profile smoke test

기본 stack에서 collector가 없어도 compose 설정이 유효한지 먼저 확인합니다.

```bash
docker compose config --quiet
```

관측성 profile 설정도 확인합니다.

```bash
docker compose --profile observability config --quiet
```

OTLP collector와 로컬 backend를 함께 실행합니다.

```bash
DEVAUTOMATION_Telemetry__Enabled=true \
  docker compose --profile observability up --build
```

API와 worker가 시작된 뒤 `/health`, ticket 생성 등을 실행합니다.

```bash
curl -s "$BASE_URL/health" | jq .
```

Jaeger와 Prometheus에서 수신 상태를 확인합니다.

```text
http://localhost:16686
http://localhost:9090
```

기대 결과:

- 기본 `docker compose config --quiet`가 collector 없이 통과합니다.
- `observability` profile config가 OTel Collector, Jaeger, Prometheus를 포함합니다.
- Jaeger에서 `DevAutomation.Api` 또는 `DevAutomation.Worker` trace를 조회할 수 있습니다.
- Prometheus에서 collector가 노출한 application/runtime metric을 조회할 수 있습니다.
- collector가 없으면 export 실패 로그가 날 수 있으므로 기본 QA에서는 telemetry를 비활성화합니다.

## 완료 체크리스트

- [ ] `tickets`, `approval_requests`, `execution_logs` table 확인
- [ ] ticket 생성 후 DB row 확인
- [ ] execution log API와 DB row 확인
- [ ] approval 상태 row 확인
- [ ] `logs/devautomation-*.log` 생성 확인
- [ ] configured secret value와 common secret pattern이 API/worker/file/Compose log에 노출되지 않음
- [ ] observability profile에서 trace/metric 조회 확인
- [ ] alerting/persistence는 미구현임을 결과에 기록

<!-- markdownlint-enable MD013 -->
