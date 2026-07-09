# QA 02. 티켓 관리 API

<!-- markdownlint-disable MD013 -->

## 목적

티켓 생성, 조회, 목록 필터, 취소, 로그 조회, 문서 생성 API가 기대대로 동작하는지
확인합니다.

## 사전 준비

먼저 [`00-local-runbook.md`](./00-local-runbook.md)를 통과합니다.

```bash
export BASE_URL=http://localhost:8080
```

처음 smoke test에서는 pre-run gate와 외부 issue/document provider를 끄면 테스트가
단순합니다.

```env
DEVAUTOMATION_ProfileReadiness__SelectedProfile=
DEVAUTOMATION_IssueTracker__Provider=None
DEVAUTOMATION_DocumentTool__Provider=None
```

주의: `POST /api/tickets`는 ticket을 저장한 뒤 Kafka에 agent job을 발행합니다. 실제
agent credential이 없으면 worker가 실행을 시도하다가 `Failed`가 될 수 있습니다. 이는
API 저장/조회 smoke test에서는 허용 가능한 결과입니다.

## TICKET-001. 티켓 생성

```bash
curl -s -X POST "$BASE_URL/api/tickets" \
  -H 'Content-Type: application/json' \
  -d '{
    "title": "QA ticket smoke test",
    "description": "Create a harmless ticket to verify API persistence and queueing.",
    "repoUrl": "https://github.com/example/repo.git",
    "baseBranch": "main",
    "createExternalIssue": false
  }' | tee /tmp/replaceme-ticket.json | jq .

export TICKET_ID=$(jq -r '.id' /tmp/replaceme-ticket.json)
echo "$TICKET_ID"
```

기대 결과:

- HTTP 201 Created입니다.
- 응답에 `id`, `title`, `description`, `repoUrl`, `baseBranch`, `status`가 있습니다.
- 최초 status는 보통 `Pending`입니다. worker가 빠르게 consume하면 곧 `Running` 또는
  `Failed`로 바뀔 수 있습니다.

## TICKET-002. 단일 티켓 조회

```bash
curl -s "$BASE_URL/api/tickets/$TICKET_ID" | jq .
```

기대 결과:

- HTTP 200입니다.
- `id`가 생성한 ticket id와 같습니다.
- 상태가 `Pending`, `Running`, `WaitingApproval`, `Completed`, `Failed`, `Cancelled`
  중 하나입니다.

## TICKET-003. 티켓 목록과 상태 필터

```bash
curl -s "$BASE_URL/api/tickets?page=1&pageSize=10" | jq .
curl -s "$BASE_URL/api/tickets?status=Failed&page=1&pageSize=10" | jq .
```

기대 결과:

- 목록은 최신 생성순으로 정렬됩니다.
- `pageSize`는 최대 100개까지 허용됩니다.
- `status`를 주면 해당 상태의 ticket만 반환합니다.

## TICKET-004. 실행 로그 조회

worker가 ticket을 처리한 뒤 실행합니다.

```bash
curl -s "$BASE_URL/api/tickets/$TICKET_ID/logs?page=1&pageSize=100" | jq .
```

기대 결과:

- ticket이 존재하면 HTTP 200입니다.
- agent가 실제로 실행되었으면 log event 배열이 반환됩니다.
- 아직 worker가 처리하지 않았거나 너무 빨리 실패했으면 빈 배열일 수 있습니다.

## TICKET-005. 취소 API

새 ticket을 만들고 가능한 빨리 취소합니다.

```bash
curl -s -X POST "$BASE_URL/api/tickets" \
  -H 'Content-Type: application/json' \
  -d '{
    "title": "QA cancel test",
    "description": "Create and cancel this ticket quickly.",
    "repoUrl": "https://github.com/example/repo.git",
    "baseBranch": "main"
  }' | tee /tmp/replaceme-cancel-ticket.json | jq .

export CANCEL_TICKET_ID=$(jq -r '.id' /tmp/replaceme-cancel-ticket.json)

curl -s -X POST "$BASE_URL/api/tickets/$CANCEL_TICKET_ID/cancel" | jq .
```

기대 결과:

- ticket이 존재하면 HTTP 200입니다.
- 응답 status는 `Cancelled`입니다.
- container가 이미 붙어 있으면 stop을 best-effort로 시도합니다.
- ticket이 이미 `Completed`라면 domain rule 때문에 실패할 수 있습니다.

## TICKET-006. pre-run gate 실패 시 ticket이 만들어지지 않는다

이 케이스는 readiness QA와 연결됩니다. `.env`에서 다음을 설정하고 required check를
일부러 실패시킨 뒤 API를 재시작합니다.

```env
DEVAUTOMATION_ProfileReadiness__SelectedProfile=personal-github-linear-notion
```

```bash
export TICKET_GATE_TITLE="QA gate block"

kafka_offset() {
  docker compose exec -T kafka kafka-run-class.sh kafka.tools.GetOffsetShell \
    --bootstrap-server kafka:9092 \
    --topic devautomation.agent-jobs \
    --time -1 2>/dev/null \
    | awk -F: '{sum += $3} END {print sum + 0}'
}

export TICKET_GATE_BEFORE_COUNT=$(docker compose exec -T postgres psql -U devautomation -d devautomation -tAc \
  "select count(*) from tickets where \"Title\" = '$TICKET_GATE_TITLE';")
export TICKET_GATE_BEFORE_OFFSET=$(kafka_offset)

curl -i -s -X POST "$BASE_URL/api/tickets" \
  -H 'Content-Type: application/json' \
  -d "{
    \"title\": \"$TICKET_GATE_TITLE\",
    \"description\": \"This should be blocked before DB save and Kafka enqueue.\",
    \"repoUrl\": \"https://github.com/example/repo.git\",
    \"baseBranch\": \"main\"
  }"

export TICKET_GATE_AFTER_COUNT=$(docker compose exec -T postgres psql -U devautomation -d devautomation -tAc \
  "select count(*) from tickets where \"Title\" = '$TICKET_GATE_TITLE';")
export TICKET_GATE_AFTER_OFFSET=$(kafka_offset)

echo "ticket count: $TICKET_GATE_BEFORE_COUNT -> $TICKET_GATE_AFTER_COUNT"
echo "kafka offset: $TICKET_GATE_BEFORE_OFFSET -> $TICKET_GATE_AFTER_OFFSET"
```

기대 결과:

- HTTP 409입니다.
- ticket count가 증가하지 않습니다.
- Kafka offset이 증가하지 않습니다.

## TICKET-007. 외부 issue 생성 설정 오류 확인

Issue tracker provider가 `None`인데 `createExternalIssue=true`이면 API가 막아야 합니다.

```bash
curl -i -s -X POST "$BASE_URL/api/tickets" \
  -H 'Content-Type: application/json' \
  -d '{
    "title": "QA external issue guard",
    "description": "Should fail because IssueTracker provider is None.",
    "repoUrl": "https://github.com/example/repo.git",
    "baseBranch": "main",
    "createExternalIssue": true
  }'
```

기대 결과:

- HTTP 400입니다.
- message는 `IssueTracker:Provider must be Jira or Linear...` 계열입니다.

## TICKET-008. 티켓 문서 생성 API

Document provider가 `None`이면 API가 명확히 실패해야 합니다.

```bash
curl -i -s -X POST "$BASE_URL/api/tickets/$TICKET_ID/documents"
```

기대 결과:

- provider가 `None`이면 HTTP 400입니다.
- Notion/Confluence가 설정되어 있으면 해당 provider에 문서를 만들고 결과를 반환합니다.
- 실제 provider 테스트는 QA용 parent page/space를 사용합니다.

## 완료 체크리스트

- [ ] ticket 생성 응답에서 id/status 확인
- [ ] 단일 ticket 조회 성공
- [ ] 목록 조회와 status filter 확인
- [ ] 실행 로그 조회 endpoint 확인
- [ ] cancel endpoint 확인
- [ ] readiness gate 실패 시 `409`와 no-ticket/no-Kafka 확인
- [ ] external issue guard 확인
- [ ] document creation endpoint의 provider별 동작 확인

<!-- markdownlint-enable MD013 -->
