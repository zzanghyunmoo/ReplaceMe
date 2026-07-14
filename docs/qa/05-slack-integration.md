# QA 05. Slack 알림과 Interactivity

<!-- markdownlint-disable MD013 -->

## 목적

Slack provider가 ticket/approval 알림을 보내고, Slack interactivity endpoint가 서명된
button payload만 처리하는지 확인합니다.

## 사전 준비

먼저 다음 QA를 통과합니다.

1. [`00-local-runbook.md`](./00-local-runbook.md)
2. [`04-approval-flow.md`](./04-approval-flow.md) 일부 또는 전체

실제 Slack callback QA는 `/api/slack/interactivity` **한 경로만** upstream으로
전달하는 path-restricted proxy/tunnel이 준비됐을 때만 실행합니다. 전체 8080 port를
공개하는 `ngrok http 8080` 같은 설정은 사용하지 않습니다. Path allowlist를 보장할 수
없으면 SLACK-005 local signed test까지만 실행하고 실제 callback QA는 중단합니다.

```bash
export BASE_URL=http://localhost:8080
# 이미 path restriction이 적용된 URL만 넣습니다.
export PUBLIC_BASE_URL=https://<protected-slack-callback-host>
```

public tunnel 안전 수칙:

- `/api/slack/interactivity` 외 path는 proxy에서 404/403으로 차단합니다.
- tunnel은 Slack QA 동안만 짧게 열고, 끝나면 즉시 종료합니다.
- tunnel이 열린 동안 provider write가 발생하는 GitHub/Linear/Notion QA를 함께 실행하지 않습니다.
- tunnel dashboard/request log에서 예상치 못한 요청이 없는지 확인합니다.

`.env` 예시:

```env
DEVAUTOMATION_Notifier__Provider=Slack
DEVAUTOMATION_Notifier__PublicBaseUrl=https://<your-tunnel>.ngrok-free.app
DEVAUTOMATION_Slack__BotToken=xoxb-...
DEVAUTOMATION_Slack__SigningSecret=<Slack signing secret>
DEVAUTOMATION_Slack__ChannelId=C0123456789
```

Broker evidence를 보존한 채 API와 worker만 build/recreate합니다.

```bash
docker compose up -d --build --force-recreate api worker
```

Slack App 설정에서 Interactivity Request URL을 다음으로 지정합니다.

```text
https://<your-tunnel>.ngrok-free.app/api/slack/interactivity
```

## SLACK-001. 설정 누락 smoke test

Slack provider를 `None`으로 두면 로컬 개발이 막히지 않아야 합니다.

```env
DEVAUTOMATION_Notifier__Provider=None
```

Host API/worker의 Ticket 상태 알림과 직접 seed한 approval row에서 기대 결과:

- API가 Slack 설정 누락 때문에 실패하지 않습니다.
- 직접 seed한 approval은 notifier를 호출하지 않습니다.
- Agent container Approval MCP에는 `Notifier=None`이 전파되지 않습니다. Full agent
  무알림 QA를 하려면 Slack token/signing secret/channel도 비워야 합니다.

## SLACK-002. 서명 없는 interactivity 요청은 거절된다

```bash
curl -i -s -X POST "$BASE_URL/api/slack/interactivity" \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode 'payload={"type":"block_actions"}'
```

기대 결과:

- HTTP 401 Unauthorized입니다.
- 서명 없는 요청은 처리하지 않습니다.

## SLACK-003. 잘못된 서명은 거절된다

```bash
curl -i -s -X POST "$BASE_URL/api/slack/interactivity" \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -H "X-Slack-Request-Timestamp: $(date +%s)" \
  -H "X-Slack-Signature: v0=invalid" \
  --data 'payload={"type":"block_actions"}'
```

기대 결과:

- HTTP 401 Unauthorized입니다.

## SLACK-004. 오래된 timestamp는 거절된다

```bash
curl -i -s -X POST "$BASE_URL/api/slack/interactivity" \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -H "X-Slack-Request-Timestamp: 1" \
  -H "X-Slack-Signature: v0=invalid" \
  --data 'payload={"type":"block_actions"}'
```

기대 결과:

- HTTP 401 Unauthorized입니다.
- replay 방지를 위해 5분보다 오래된 timestamp는 실패해야 합니다.

## SLACK-005. 정상 서명 payload local positive test

실제 Slack 버튼 없이도 서명 검증과 payload 처리 성공 경로를 확인합니다. 먼저
[`04-approval-flow.md`](./04-approval-flow.md)의 seed 방식으로 pending approval을 만들고
`APPROVAL_ID`를 export합니다.

```bash
read -rsp "Slack signing secret: " SLACK_SIGNING_SECRET; echo
export SLACK_SIGNING_SECRET
export SLACK_TS=$(date +%s)
export SLACK_BODY=$(python3 - <<'PY'
import json
import os
import urllib.parse
payload = {
    "user": {"id": "UQALOCAL"},
    "actions": [{"action_id": "approval_approve", "value": os.environ["APPROVAL_ID"]}],
}
print("payload=" + urllib.parse.quote(json.dumps(payload, separators=(",", ":"))))
PY
)
export SLACK_SIG=$(python3 - <<'PY'
import hashlib
import hmac
import os
secret = os.environ['SLACK_SIGNING_SECRET'].encode()
base = f"v0:{os.environ['SLACK_TS']}:{os.environ['SLACK_BODY']}".encode()
print('v0=' + hmac.new(secret, base, hashlib.sha256).hexdigest())
PY
)

curl -i -s -X POST "$BASE_URL/api/slack/interactivity" \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -H "X-Slack-Request-Timestamp: $SLACK_TS" \
  -H "X-Slack-Signature: $SLACK_SIG" \
  --data "$SLACK_BODY"

curl -s "$BASE_URL/api/approvals?page=1&pageSize=20" \
  | jq ".[] | select(.id == \"$APPROVAL_ID\")"

unset SLACK_SIGNING_SECRET SLACK_BODY SLACK_SIG SLACK_TS
```

기대 결과:

- HTTP 200입니다.
- seeded approval status가 `Approved`로 바뀝니다.
- signing secret은 화면에 echo되지 않고, 테스트 후 shell 환경에서 제거됩니다.

## SLACK-006. 실제 Slack ticket 상태 알림

Slack credential을 설정한 뒤 ticket을 생성합니다.

```bash
curl -s -X POST "$BASE_URL/api/tickets" \
  -H 'Content-Type: application/json' \
  -d '{
    "title": "QA Slack notification",
    "description": "Verify Slack ticket status messages.",
    "repoUrl": "https://github.com/example/not-a-real-repo-for-slack-qa.git",
    "baseBranch": "main"
  }' | tee /tmp/replaceme-slack-ticket.json | jq .
```

기대 결과:

- Slack channel에 ticket status 메시지가 전송됩니다.
- 실패 경로 smoke test라면 최종적으로 `Ticket Failed` 메시지가 전송됩니다.
- API 로그에 Slack API 오류가 없어야 합니다.

## SLACK-007. 실제 Slack 승인 버튼

[`04-approval-flow.md`](./04-approval-flow.md)의 pending approval 생성 절차를 수행합니다.

기대 결과:

- Slack channel에 승인/거절 버튼이 있는 메시지가 나타납니다.
- `[승인]` 클릭 시 approval status가 `Approved`가 됩니다.
- `[거절]` 클릭 시 approval status가 `Rejected`가 됩니다.
- 원본 Slack 메시지가 결과 요약으로 update됩니다.

확인 명령:

```bash
curl -s "$BASE_URL/api/approvals?page=1&pageSize=20" | jq .
```

## SLACK-008. 실제 Slack payload replay 보조 명령

Slack이 보낸 실제 payload가 있을 때 사용하는 보조 명령입니다. `payload=...` 형태의
body와 signing secret으로 signature를 계산해 로컬 endpoint에 replay할 수 있습니다.
단, timestamp가 5분을 넘으면 실패합니다.

```bash
read -rsp "Slack signing secret: " SLACK_SIGNING_SECRET; echo
export SLACK_SIGNING_SECRET
export SLACK_BODY='payload=<url-encoded-real-slack-payload>'
export SLACK_TS=$(date +%s)
export SLACK_SIG=$(python3 - <<'PY'
import hashlib
import hmac
import os
secret = os.environ['SLACK_SIGNING_SECRET'].encode()
base = f"v0:{os.environ['SLACK_TS']}:{os.environ['SLACK_BODY']}".encode()
print('v0=' + hmac.new(secret, base, hashlib.sha256).hexdigest())
PY
)

curl -i -s -X POST "$BASE_URL/api/slack/interactivity" \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -H "X-Slack-Request-Timestamp: $SLACK_TS" \
  -H "X-Slack-Signature: $SLACK_SIG" \
  --data "$SLACK_BODY"

unset SLACK_SIGNING_SECRET SLACK_BODY SLACK_SIG SLACK_TS
```

## 완료 체크리스트

- [ ] Slack provider `None`일 때 로컬 개발이 막히지 않음
- [ ] 서명 없는 interactivity 요청은 401
- [ ] 잘못된 서명은 401
- [ ] 오래된 timestamp는 401
- [ ] 정상 서명 local positive test가 HTTP 200과 approval `Approved`를 반환
- [ ] 실제 ticket status가 Slack channel에 전송됨
- [ ] approval 버튼 클릭이 `Approved`/`Rejected`로 반영됨
- [ ] 원본 Slack 메시지가 결과 요약으로 update됨
- [ ] 테스트 후 public tunnel과 Slack signing secret 환경변수를 정리함

<!-- markdownlint-enable MD013 -->
