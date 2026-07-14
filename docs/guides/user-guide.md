# 사용자 가이드

Notion canonical:
[사용자 가이드](https://app.notion.com/p/398ef22ad4fc81ab98b3ef52079c75be)

기준일: 2026-07-14

> ReplaceMe는 현재 개인 로컬 개발 자동화 실험용입니다. 운영자가 준비한 trusted
> local 환경과 QA용 저장소에서만 사용합니다.

## 할 수 있는 일

- 개발 요청을 Ticket으로 등록하고 상태·로그 조회
- worker가 Docker container에서 Claude Code 실행
- GitHub PR 또는 GitLab MR 생성 시도
- 민감 작업의 승인/거절
- Run Passport v1 ticket-scoped 실행 요약 조회
- 선택적으로 Linear/Jira issue와 Notion/Confluence 문서 연결

아직 인증된 다중 사용자 서비스, production-grade runner, run replay UI, 자동 Notion
lifecycle/PR packet publication, alerting/DLQ replay UI는 없습니다.

## 1. 가장 안전한 첫 실행

운영자가 API, PostgreSQL, Kafka를 worker 없이 시작합니다.

```bash
docker compose up -d --build api postgres kafka
curl -fsS http://localhost:8080/health | jq .
```

worker가 없으면 Ticket은 queue에 남습니다. 단순 API smoke라면 생성 후 즉시
cancel합니다.

## 2. 좋은 Ticket 작성

다음을 포함합니다.

- 대상 repo URL과 base branch
- 원하는 변경
- 성공 조건
- 실행할 테스트
- 건드리면 안 되는 범위
- 필요한 external issue reference

```bash
curl -sS -X POST http://localhost:8080/api/tickets \
  -H 'Content-Type: application/json' \
  -d '{
    "title": "README 설명 보완",
    "description": "설치 설명을 보완하고 기존 테스트를 실행한다.",
    "repoUrl": "https://github.com/<owner>/<scratch-repo>.git",
    "baseBranch": "main"
  }' | tee /tmp/replaceme-ticket.json | jq .

export TICKET_ID=$(jq -r .id /tmp/replaceme-ticket.json)

# API-only smoke라면 worker를 시작하기 전에 즉시 취소합니다.
curl -sS -X POST \
  http://localhost:8080/api/tickets/$TICKET_ID/cancel | jq .
```

API-only smoke가 아니라 실제 agent 실행을 원하면 이 단계에서 취소하지 않습니다.

## 3. 상태와 결과 확인

```bash
curl -sS http://localhost:8080/api/tickets/$TICKET_ID | jq .
curl -sS http://localhost:8080/api/tickets/$TICKET_ID/logs | jq .
curl -sS http://localhost:8080/api/tickets/$TICKET_ID/run-passport | jq .
```

상태는 `Pending`, `Running`, `WaitingApproval`, `Completed`, `Failed`,
`Cancelled` 중 하나입니다. Run Passport ID는 개별 attempt/rerun ID가 아니라 같은
Ticket workflow의 mutable key입니다. `runPassportUrl`은 상대 경로이므로 외부 문서에
링크할 때는 운영자가 지정한 API base URL과 결합합니다. `lastLifecycleAt`도 일반적인
수정 시각이 아니라 created/started/completed 중 마지막으로 알려진 시각입니다.

## 4. 실제 agent 실행

scratch repo와 최소 권한 token을 준비합니다. 앞에서 API-only smoke Ticket을
취소했다면 section 2의 template으로 **새 Ticket**을 만들고 새 `TICKET_ID`를
export한 뒤 worker를 시작합니다.

```bash
docker compose --profile build-only build agent-image
docker compose up -d --build worker
```

성공하면 `agent/ticket-<id>` branch와 PR/MR URL이 생길 수 있습니다. Container exit
code 0이어도 변경이나 PR이 없을 수 있으므로 diff와 test evidence를 직접 확인합니다.

## 5. 승인 처리

```bash
curl -sS 'http://localhost:8080/api/approvals?status=Pending' | jq .
curl -sS -X POST \
  http://localhost:8080/api/approvals/<approval-id>/approve | jq .

curl -sS -X POST http://localhost:8080/api/approvals/<approval-id>/reject \
  -H 'Content-Type: application/json' \
  -d '{"reason":"지금 실행하면 안 됨"}' | jq .
```

Slack을 쓰면 버튼으로 처리할 수 있습니다. 승인 API에는 아직 인증이 없으므로
local-only입니다.

## 6. 취소

```bash
curl -sS -X POST \
  http://localhost:8080/api/tickets/$TICKET_ID/cancel | jq .
```

취소는 container stop을 best-effort로 시도합니다. 최종 Ticket 상태와 남은 container를
운영자와 확인합니다.

## 7. 성공 판정

- Ticket이 기대 terminal 상태인가
- PR/MR 링크와 diff가 요청 범위와 맞는가
- 테스트 결과가 실제로 남았는가
- secret/개인 경로가 노출되지 않았는가
- 미실행 검증과 residual risk가 기록됐는가

## 8. 장애 전달 정보

운영자에게 Ticket ID, status, `failReason`, 마지막 의미 있는 log, branch/PR URL,
발생 시각과 환경을 전달합니다. Secret 원문은 전달하지 않습니다.

## 안전 수칙

- 중요 repo/main이 아니라 scratch repo에서 먼저 검증합니다.
- public tunnel로 전체 API를 노출하지 않습니다.
- token은 최소 scope·짧은 수명을 사용합니다.
- `docker compose down` 뒤 queued job이 사라질 수 있으므로 Pending ticket을 먼저
  확인합니다.
