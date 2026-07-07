# 에이전트 실행 워커

## 무엇을 하는 기능인가

티켓이 생성되면 Hangfire가 `AgentJob.RunAsync(ticketId)`를 `agents` 큐에서
실행합니다. 이 job은 티켓 상태를 `Running`으로 바꾸고, Docker 컨테이너 하나를
생성해 Claude Code headless 작업을 격리 실행합니다.

## 실행 흐름

```text
POST /api/tickets
  -> Ticket 저장
  -> Hangfire agents queue enqueue
  -> AgentJob.RunAsync
  -> DockerAgentRunner.RunAsync
  -> container: git clone / branch 생성 / claude -p 실행
  -> 로그 stream-json 파싱 및 DB 저장
  -> commit/push/PR 생성 시도
  -> Ticket Completed 또는 Failed
  -> container force remove
```

## 컨테이너 안에서 하는 일

`DockerAgentRunner`는 agent image에서 다음 스크립트를 실행합니다.

1. `/work` 아래에 repository clone
2. `BASE_BRANCH` checkout
3. `agent/ticket-${TICKET_ID}` 브랜치 생성
4. Claude Code MCP 설정 파일 생성
5. `claude -p "$TICKET_PROMPT" --output-format stream-json ...` 실행
6. 변경 사항이 있으면 commit/push
7. `gh pr create`가 가능하면 PR 생성 후 `PR_URL=...` 로그 출력

## 주요 설정

<!-- markdownlint-disable MD013 -->
| 설정 | 기본값 | 설명 |
| --- | --- | --- |
| `Agent:MaxConcurrentAgents` | `2` | Hangfire worker count |
| `Agent:AgentTimeout` | `00:30:00` | 티켓당 최대 실행 시간 |
| `Agent:ClaudeImage` | `devautomation-claude:latest` | agent container image |
| `Agent:DockerNetwork` | `bridge` | agent container Docker network |
| `Agent:ApprovalMcpCommand` | `dotnet /app/DevAutomation.ApprovalMcp.dll` | MCP server 실행 명령 |
<!-- markdownlint-enable MD013 -->

Docker Compose 실행 시 `DockerNetwork`는 `devautomation-network`로 지정되어
agent container가 PostgreSQL, Slack 설정, MCP server 실행 환경을 사용할 수
있게 합니다.

## 로그 처리

- Docker stdout/stderr stream을 실시간으로 읽습니다.
- 각 줄은 `ClaudeStreamParser`를 거쳐 `AgentLogEvent`로 변환됩니다.
- JSON line이면 `type` 필드를 event type으로 사용합니다.
- plain text면 `stdout` event로 저장합니다.
- `SecretRedactor`가 Anthropic, GitHub, Slack 관련 secret 값을
  `[REDACTED]`로 치환합니다.
- `AgentJob`은 로그를 25개씩 buffer 후 DB에 저장합니다.

## 코드 위치

- Hangfire job orchestration: `src/DevAutomation.Infrastructure/Agents/AgentJob.cs`
- Docker 실행: `src/DevAutomation.Infrastructure/Agents/DockerAgentRunner.cs`
- stream-json parser: `src/DevAutomation.Infrastructure/Agents/ClaudeStreamParser.cs`
- secret redaction: `src/DevAutomation.Infrastructure/Agents/SecretRedactor.cs`
- agent option: `src/DevAutomation.Core/Options/AgentOptions.cs`

## 확인 방법

```bash
# agent image build
docker compose --profile build-only build agent-image

# API + DB + Redis 실행
docker compose up --build api postgres redis

# 티켓 생성 후 Hangfire dashboard 확인
open http://localhost:8080/hangfire
```

## 현재 한계

- 실패 시 자동 재시도는 하지 않습니다.
- 컨테이너 권한, network policy, Docker socket 접근 제한은 운영 환경에서
  별도 hardening이 필요합니다.
- PR 생성은 `gh` CLI와 GitHub token이 agent image 안에서 정상 동작할 때만
  가능합니다.
