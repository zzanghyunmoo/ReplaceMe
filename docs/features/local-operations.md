# 로컬 실행과 운영 확인

## 무엇을 하는 기능인가

ReplaceMe는 Docker Compose로 API, PostgreSQL, Redis, agent image를 함께 실행할
수 있게 구성되어 있습니다. `/health` endpoint와 Hangfire dashboard로 기본
연결 상태와 job 상태를 확인합니다.

## 실행 구성

```text
docker-compose.yml
  api          -> ASP.NET Core API + Hangfire server
  postgres    -> ticket/approval/log 저장소
  redis       -> Redis 연결 확인용 dependency
  agent-image -> Claude Code + Approval MCP 포함 agent image build target
```

## 빠른 실행

```bash
cp .env.example .env
# .env에 필요한 token/channel 값 입력

docker compose --profile build-only build agent-image
docker compose up --build api postgres redis
```

API는 기본적으로 다음 주소에서 열립니다.

```text
http://localhost:8080
```

Hangfire dashboard:

```text
http://localhost:8080/hangfire
```

## 환경변수

<!-- markdownlint-disable MD013 -->
| 환경변수 | 설명 |
| --- | --- |
| `DEVAUTOMATION_Agent__AnthropicApiKey` | agent container에 주입할 Anthropic API key |
| `DEVAUTOMATION_Agent__GitHubToken` | agent가 push/PR 생성에 사용할 GitHub token |
| `DEVAUTOMATION_Agent__DockerNetwork` | agent container가 붙을 Docker network |
| `DEVAUTOMATION_Slack__BotToken` | Slack Web API bot token |
| `DEVAUTOMATION_Slack__SigningSecret` | Slack interactivity signature 검증 secret |
| `DEVAUTOMATION_Slack__ChannelId` | 승인/알림을 보낼 Slack channel |
<!-- markdownlint-enable MD013 -->

`appsettings.json`에는 민감값을 넣지 않고, `.env` 또는 runtime environment로
주입합니다.

## Health check

`GET /health`는 다음 dependency를 확인합니다.

| 항목 | 확인 방식 |
| --- | --- |
| DB | `dbContext.Database.CanConnectAsync()` |
| Redis | `ConnectionMultiplexer.ConnectAsync(...).PingAsync()` |
| Docker | `DockerClient.System.PingAsync()` |

모두 정상이면 `200 OK`, 하나라도 실패하면 `Problem` 응답을 반환합니다.

## 개발 검증 명령

```bash
dotnet restore DevAutomation.sln
dotnet build DevAutomation.sln
dotnet test DevAutomation.sln
```

로컬 머신에 .NET 8 runtime이 없다면 Docker SDK 이미지로 테스트할 수 있습니다.

```bash
docker run --rm -v "$PWD":/src -w /src \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test DevAutomation.sln --no-restore
```

## 코드 위치

- Compose: `docker-compose.yml`
- API image: `Dockerfile`
- Agent image: `Dockerfile.agent`
- 설정: `src/DevAutomation.Api/appsettings.json`, `.env.example`
- Health endpoint: `src/DevAutomation.Api/Program.cs`

## 현재 한계

- production deployment manifest는 아직 없습니다.
- Docker socket mount는 로컬 개발용이며, 운영에서는 별도 격리가 필요합니다.
- Redis는 현재 Hangfire storage가 아니라 health dependency로만 확인합니다.
  Hangfire storage는 PostgreSQL입니다.
