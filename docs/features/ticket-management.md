# 티켓 관리 API

## 무엇을 하는 기능인가

사용자가 개발 요구사항을 티켓으로 등록하면 ReplaceMe가 이를 DB에 저장하고,
즉시 Hangfire `agents` 큐에 에이전트 실행 잡을 등록합니다. 이후 API로 티켓
상태, 목록, 로그를 확인하거나 실행 중인 티켓을 취소할 수 있습니다.

## 구현된 엔드포인트

| 메서드 | 경로 | 설명 |
| --- | --- | --- |
| `POST` | `/api/tickets` | 티켓 생성 + Hangfire agent job enqueue |
| `GET` | `/api/tickets/{id}` | 단일 티켓 상태 조회 |
| `GET` | `/api/tickets` | 상태 필터와 페이징을 지원하는 목록 조회 |
| `POST` | `/api/tickets/{id}/cancel` | 티켓 취소 + 연결된 컨테이너 중지 시도 |
| `GET` | `/api/tickets/{id}/logs` | 티켓별 실행 로그 조회 |

## 요청/응답 모델

티켓 생성 요청은 `CreateTicketRequest`를 사용합니다.

```json
{
  "title": "Add login API",
  "description": "Implement login endpoint and tests",
  "repoUrl": "https://github.com/org/repo.git",
  "baseBranch": "main"
}
```

응답은 `TicketResponse` 형태로 티켓의 현재 상태와 PR URL, 실패 사유를
반환합니다.

## 상태 전이

현재 티켓 상태는 다음 흐름을 지원합니다.

```text
Pending -> Running -> WaitingApproval -> Running -> Completed
Pending -> Running -> Failed
Pending/Running/WaitingApproval -> Cancelled
```

- `Pending`: 티켓 생성 직후
- `Running`: Hangfire job이 시작되어 agent container가 실행 중
- `WaitingApproval`: MCP approval tool이 Slack 승인을 기다리는 중
- `Completed`: agent container가 성공 종료하고 PR URL을 기록한 상태
- `Failed`: agent timeout, container exit code, 예외 발생
- `Cancelled`: 사용자가 취소 API를 호출한 상태

## 코드 위치

- API 라우팅: `src/DevAutomation.Api/Program.cs`
- 요청/응답 contract: `src/DevAutomation.Core/Contracts/TicketContracts.cs`
- 도메인 엔티티: `src/DevAutomation.Core/Entities/Ticket.cs`
- 상태 전이 서비스: `src/DevAutomation.Core/Services/TicketStateMachine.cs`
- 실행 잡: `src/DevAutomation.Infrastructure/Agents/AgentJob.cs`

## 확인 방법

```bash
curl -X POST http://localhost:8080/api/tickets \
  -H 'Content-Type: application/json' \
  -d '{
    "title": "Update README",
    "description": "Add setup instructions and tests",
    "repoUrl": "https://github.com/example/repo.git",
    "baseBranch": "main"
  }'

curl http://localhost:8080/api/tickets
curl http://localhost:8080/api/tickets/{ticket-id}
curl http://localhost:8080/api/tickets/{ticket-id}/logs
```

## 현재 한계

- API 인증/인가가 아직 없습니다.
- 실패한 티켓 재실행 API는 아직 없습니다.
- `cancel`은 티켓 상태를 먼저 취소로 바꾸고, 이후 Docker container stop을
  best-effort로 시도합니다.
