# QA 04. Approval MCP와 수동 승인 API

<!-- markdownlint-disable MD013 -->

## 목적

코딩 에이전트가 민감 작업 전에 `approval_prompt` MCP tool을 호출했을 때 승인 요청이
저장되고, 승인/거절 API 또는 Slack 버튼 결과가 agent에 반영되는지 확인합니다.

## 사전 준비

먼저 다음 QA를 통과합니다.

1. [`00-local-runbook.md`](./00-local-runbook.md)
2. [`03-agent-execution.md`](./03-agent-execution.md)의 agent image build

기본 변수:

```bash
export BASE_URL=http://localhost:8080
```

Host API/worker의 notifier를 `None`으로 둘 수 있지만, 이 값은 agent container의
Approval MCP에 전달되지 않습니다. 직접 seed한 row의 API 상태 전이만 확인할 때는
notifier를 호출하지 않습니다. Full agent 무알림 QA에서는 Slack 값도 비웁니다.

```env
DEVAUTOMATION_Notifier__Provider=None
DEVAUTOMATION_Slack__BotToken=
DEVAUTOMATION_Slack__SigningSecret=
DEVAUTOMATION_Slack__ChannelId=
```

Slack 버튼까지 확인하려면 [`05-slack-integration.md`](./05-slack-integration.md)를 먼저
준비합니다.

## APPROVAL-001. 승인 목록 조회

```bash
curl -s "$BASE_URL/api/approvals?page=1&pageSize=20" | jq .
```

기대 결과:

- HTTP 200입니다.
- approval request 배열을 반환합니다.
- 아직 승인 요청이 없으면 빈 배열입니다.

상태 필터:

```bash
curl -s "$BASE_URL/api/approvals?status=Pending&page=1&pageSize=20" | jq .
```

## APPROVAL-002. 잘못된 approval id 처리

```bash
curl -i -s -X POST "$BASE_URL/api/approvals/00000000-0000-0000-0000-000000000000/approve"
```

기대 결과:

- HTTP 404입니다.

## APPROVAL-003. Pending approval 만들기

### 결정적 seed 방식

수동 승인/거절 API를 안정적으로 확인하려면 pending approval row를 직접 seed합니다.
이 방식은 Approval MCP end-to-end를 검증하지는 않지만, APPROVAL-004부터
APPROVAL-006까지의 상태 전이 QA를 결정적으로 만들 수 있습니다.

먼저 기준 ticket을 만듭니다.

```bash
curl -s -X POST "$BASE_URL/api/tickets" \
  -H 'Content-Type: application/json' \
  -d '{
    "title": "QA approval seed ticket",
    "description": "Ticket used to seed approval request rows for deterministic QA.",
    "repoUrl": "https://github.com/example/repo.git",
    "baseBranch": "main"
  }' | tee /tmp/replaceme-approval-ticket.json | jq .

export APPROVAL_TICKET_ID=$(jq -r '.id' /tmp/replaceme-approval-ticket.json)
export APPROVAL_ID=$(uuidgen | tr '[:upper:]' '[:lower:]')
```

pending approval을 seed합니다.

```bash
docker compose exec -T postgres psql -U devautomation -d devautomation <<SQL
insert into approval_requests
  ("Id", "TicketId", "ToolName", "InputJson", "Status", "RequestedAt")
values
  ('$APPROVAL_ID', '$APPROVAL_TICKET_ID', 'qa.seed', '{}'::jsonb, 'Pending', now());
SQL

curl -s "$BASE_URL/api/approvals?status=Pending&page=1&pageSize=20" | jq .
```

기대 결과:

- `/api/approvals?status=Pending`에 `$APPROVAL_ID`가 나타납니다.
- 이 seed row는 notifier 메시지를 보내지 않습니다.

### Full agent 방식

Approval MCP end-to-end까지 확인하려면 full agent run 중 Claude Code permission prompt가
발생하게 합니다. 이 방식은 실제 동작에 가깝지만, agent가 prompt를 만들지 않으면 pending
approval이 생기지 않을 수 있습니다.

`DockerAgentRunner`는 Claude Code를 다음 옵션으로 실행합니다.

```text
--mcp-config /tmp/claude-mcp.json
--strict-mcp-config
--permission-prompt-tool mcp__approval__approval_prompt
```

scratch repo를 대상으로 agent ticket을 만들고, prompt에 “파일을 수정하기 전에 필요한
권한 요청이 나오면 승인 요청을 사용하라”는 내용을 넣습니다.

```bash
export QA_REPO_URL=https://github.com/<owner>/<scratch-repo>.git

curl -s -X POST "$BASE_URL/api/tickets" \
  -H 'Content-Type: application/json' \
  -d "{
    \"title\": \"QA approval prompt\",
    \"description\": \"Before modifying files, proceed through the configured permission prompt. Then add QA-APPROVAL.md with one sentence.\",
    \"repoUrl\": \"$QA_REPO_URL\",
    \"baseBranch\": \"main\"
  }" | tee /tmp/replaceme-approval-ticket.json | jq .

export APPROVAL_TICKET_ID=$(jq -r '.id' /tmp/replaceme-approval-ticket.json)
```

approval이 생기는지 조회합니다.

```bash
watch -n 2 "curl -s $BASE_URL/api/approvals?status=Pending&page=1&pageSize=20 | jq ."
```

기대 결과:

- ticket 상태가 `WaitingApproval`로 바뀝니다.
- `/api/approvals?status=Pending`에 새 approval request가 나타납니다.
- Slack 설정이 비어 있으면 agent는 DB polling을 계속하며 Slack message reference는
  `not-configured` 계열일 수 있습니다.
- Slack을 설정했다면 Slack channel에 승인/거절 버튼 메시지가 나타납니다.

## APPROVAL-004. 수동 승인

Pending approval id를 변수에 넣습니다.

```bash
export APPROVAL_ID=<pending-approval-id>

curl -s -X POST "$BASE_URL/api/approvals/$APPROVAL_ID/approve" | jq .
```

기대 결과:

- HTTP 200입니다.
- approval status는 `Approved`입니다.
- `respondedAt`이 채워집니다.
- seed 방식에서는 approval row만 `Approved`로 바뀝니다.
- full agent 방식에서는 agent에 `{ "behavior": "allow" }` 계열 응답이 반환됩니다.
- full agent 방식에서는 ticket이 `WaitingApproval`에서 다시 `Running`으로 돌아갑니다.

## APPROVAL-005. 수동 거절

새 pending approval을 만든 뒤 실행합니다.

```bash
export APPROVAL_ID=<pending-approval-id>

curl -s -X POST "$BASE_URL/api/approvals/$APPROVAL_ID/reject" \
  -H 'Content-Type: application/json' \
  -d '{ "reason": "QA reject path" }' | jq .
```

기대 결과:

- HTTP 200입니다.
- approval status는 `Rejected`입니다.
- `responseReason`에 `QA reject path`가 들어갑니다.
- seed 방식에서는 approval row만 `Rejected`로 바뀝니다.
- full agent 방식에서는 agent에 `{ "behavior": "deny" }` 계열 응답이 반환됩니다.

## APPROVAL-006. terminal approval 재처리 방지

이미 승인 또는 거절된 approval에 다시 approve/reject를 호출합니다.

```bash
curl -i -s -X POST "$BASE_URL/api/approvals/$APPROVAL_ID/approve"
```

기대 결과:

- HTTP 409 Conflict입니다.
- 이미 terminal 상태인 approval response를 반환합니다.

## APPROVAL-007. Timeout 확인

빠르게 timeout을 확인하려면 `.env`에서 timeout을 짧게 설정합니다. 이 값은 worker가
새 agent container에 전달하고 Approval MCP가 그 container 안에서 읽으므로, API만
재시작해서는 반영되지 않습니다.

```env
DEVAUTOMATION_Approval__ApprovalTimeout=00:00:10
DEVAUTOMATION_Approval__PollInterval=00:00:01
```

worker를 재생성하고 **새 agent run**에서 Pending approval을 만든 뒤 아무 응답도
하지 않습니다.

```bash
docker compose up -d --no-deps --force-recreate worker
```

직접 seed한 row만으로는 ApprovalService polling loop가 시작되지 않으므로 timeout
end-to-end 판정에 사용하지 않습니다.

기대 결과:

- deadline 이후 approval status가 `TimedOut`이 됩니다.
- agent에는 deny 응답이 반환됩니다.
- ticket은 이후 agent 판단에 따라 `Running`으로 돌아가거나 실패할 수 있습니다.

## 완료 체크리스트

- [ ] approval 목록 조회 성공
- [ ] 없는 approval id는 404
- [ ] full agent run 중 pending approval 생성
- [ ] 수동 approve가 `Approved`로 반영
- [ ] 수동 reject가 `Rejected`와 reason으로 반영
- [ ] terminal approval 재처리는 409
- [ ] timeout이 `TimedOut`과 deny로 반영
- [ ] ticket 상태가 `Running -> WaitingApproval -> Running` 흐름을 보임

<!-- markdownlint-enable MD013 -->
