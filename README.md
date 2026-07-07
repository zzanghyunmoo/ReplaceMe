# ReplaceMe / DevAutomation

.NET 8 기반 개발 자동화 오케스트레이션 서버입니다. 티켓을 API로 입력하면
Hangfire 잡이 Docker 컨테이너에서 `claude -p`를 실행하고, 민감 작업은 MCP
approval tool을 통해 Slack 승인 버튼을 받은 뒤 계속 진행합니다.

## 구성

- `src/DevAutomation.Api` — ASP.NET Core Minimal API, Hangfire dashboard,
  Slack interactivity webhook
- `src/DevAutomation.Core` — 도메인 모델, 옵션, 인터페이스,
  승인/상태 전이 서비스
- `src/DevAutomation.Infrastructure` — EF Core/PostgreSQL, Docker.DotNet
  agent runner, Slack Web API 구현
- `src/DevAutomation.ApprovalMcp` — `approval_prompt` MCP stdio 서버
- `tests/DevAutomation.Tests` — 승인 플로우와 티켓 상태 전이 단위 테스트

## 빠른 실행

```bash
cp .env.example .env
# .env에 ANTHROPIC_API_KEY, GITHUB_TOKEN,
# Slack bot token/signing secret/channel id 입력

docker compose --profile build-only build agent-image
docker compose up --build api postgres redis
```

API는 `http://localhost:8080`에서 실행됩니다. Hangfire dashboard는
`/hangfire`입니다. 에이전트 컨테이너는 같은 Docker 네트워크
(`devautomation-network`)에 붙어 승인 MCP 서버가 PostgreSQL과 Slack 설정을
사용할 수 있게 구성됩니다.

## 주요 API

```http
POST /api/tickets
{
  "title": "Add login API",
  "description": "Implement login endpoint and tests",
  "repoUrl": "https://github.com/org/repo.git",
  "baseBranch": "main"
}

GET /api/tickets/{id}
GET /api/tickets?status=Running&page=1&pageSize=20
POST /api/tickets/{id}/cancel
GET /api/tickets/{id}/logs?page=1&pageSize=100
GET /api/approvals
POST /api/slack/interactivity
GET /health
```

## 설정

모든 민감값은 `appsettings.json`이 아니라 환경변수 또는 `.env`로 주입합니다.

- `DEVAUTOMATION_Agent__MaxConcurrentAgents` 기본 2
- `DEVAUTOMATION_Agent__AgentTimeout` 기본 `00:30:00`
- `DEVAUTOMATION_Agent__DockerNetwork` 기본 `bridge`, compose 실행 시
  `devautomation-network`
- `DEVAUTOMATION_Agent__ApprovalMcpCommand` 기본
  `dotnet /app/DevAutomation.ApprovalMcp.dll`
- `DEVAUTOMATION_Approval__ApprovalTimeout` 기본 `00:10:00`
- `DEVAUTOMATION_Slack__BotToken`
- `DEVAUTOMATION_Slack__SigningSecret`
- `DEVAUTOMATION_Slack__ChannelId`

## 검증

```bash
dotnet restore DevAutomation.sln
dotnet build DevAutomation.sln
dotnet test DevAutomation.sln
```

## 보안 메모

- Slack interactivity는 `X-Slack-Signature`와 `X-Slack-Request-Timestamp`를
  검증합니다.
- Agent runner는 Docker 컨테이너를 티켓별 1개 생성하고 종료 후 강제 삭제합니다.
- `ANTHROPIC_API_KEY`, `GITHUB_TOKEN`, Slack bot token은 로그 저장 전
  redaction 대상입니다.
- 운영에서는 agent image의 네트워크/볼륨 권한과 Docker socket 접근을 별도
  격리 계층으로 제한하세요.
