# ReplaceMe / DevAutomation

.NET 8 기반 개발 자동화 오케스트레이션 서버입니다. 티켓을 API로 입력하면
Kafka 큐를 통해 Docker 컨테이너에서 코딩 에이전트를 실행하고, 민감 작업은 MCP
approval tool을 통해 active notifier의 승인 알림을 받은 뒤 계속 진행합니다.

## 구성

- `src/DevAutomation.Api` — ASP.NET Core Minimal API, Kafka producer/worker,
  Slack interactivity webhook, OpenTelemetry 설정
- `src/DevAutomation.Core` — 도메인 모델, 옵션, provider 인터페이스,
  승인/상태 전이 서비스
- `src/DevAutomation.Infrastructure` — EF Core/PostgreSQL, Kafka queue,
  Docker.DotNet agent runner, provider별 외부 연동 구현
- `src/DevAutomation.ApprovalMcp` — `approval_prompt` MCP stdio 서버
- `tests/DevAutomation.Tests` — 승인 플로우와 티켓 상태 전이 단위 테스트

## 지원 연동

고정 인프라:

- Database: PostgreSQL
- Message queue API: Kafka
- Local Docker broker: Redpanda (Kafka-compatible, service name `kafka`)

단일 active provider 선택형 연동:

- Issue tracker: `Jira`, `Linear`, `None`
- Remote repository: `GitHub`, `GitLab`
- Notifier: `Slack`, `Gmail`, `None`
- Document tool: `Notion`, `Confluence`, `None`
- Coding agent: `ClaudeCode`

## 빠른 실행

```bash
cp .env.example .env
# .env에 Anthropic/GitHub 또는 GitLab, notifier, issue tracker, document tool 값을 입력

docker compose --profile build-only build agent-image
docker compose up --build api postgres kafka
```

API는 `http://localhost:8080`에서 실행됩니다. Compose의 `kafka` 서비스는
Redpanda를 Kafka-compatible broker로 실행하므로 애플리케이션 설정은 기존
`kafka:9092` bootstrap server를 그대로 사용합니다. 에이전트 컨테이너는 같은 Docker
네트워크(`devautomation-network`)에 붙어 승인 MCP 서버가 PostgreSQL과 notifier
설정을 사용할 수 있게 구성됩니다.

## 주요 API

```http
POST /api/tickets
{
  "title": "Add login API",
  "description": "Implement login endpoint and tests",
  "repoUrl": "https://github.com/org/repo.git",
  "baseBranch": "main",
  "createExternalIssue": false
}

GET /api/tickets/{id}
GET /api/tickets/{id}/run-passport
GET /api/tickets?status=Running&page=1&pageSize=20
POST /api/tickets/{id}/cancel
GET /api/tickets/{id}/logs?page=1&pageSize=100
POST /api/tickets/{id}/documents
GET /api/approvals
POST /api/approvals/{id}/approve
POST /api/approvals/{id}/reject
POST /api/slack/interactivity
GET /health
```

## 설정

모든 민감값은 `appsettings.json`이 아니라 환경변수 또는 `.env`로 주입합니다.

- `DEVAUTOMATION_Queue__KafkaBootstrapServers`
- `DEVAUTOMATION_Agent__RemoteRepositoryProvider` — `GitHub` 또는 `GitLab`
- `DEVAUTOMATION_Agent__GitHubToken`
- `DEVAUTOMATION_Agent__GitLabToken`
- `DEVAUTOMATION_Notifier__Provider` — `Slack`, `Gmail`, `None`
- `DEVAUTOMATION_IssueTracker__Provider` — `Jira`, `Linear`, `None`
- `DEVAUTOMATION_DocumentTool__Provider` — `Notion`, `Confluence`, `None`
- `DEVAUTOMATION_Telemetry__Enabled`
- `DEVAUTOMATION_Telemetry__OtlpEndpoint`

자세한 값은 `.env.example`을 참고하세요.

## 검증

```bash
dotnet restore DevAutomation.sln
dotnet build DevAutomation.sln
dotnet test DevAutomation.sln
```

로컬에 .NET 8 runtime이 없으면 Docker SDK 이미지로 테스트할 수 있습니다.

```bash
docker run --rm -v "$PWD":/src -w /src \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test DevAutomation.sln --no-restore
```

## 보안 메모

- Slack interactivity는 `X-Slack-Signature`와 `X-Slack-Request-Timestamp`를
  검증합니다.
- Gmail notifier는 Gmail API access token을 사용합니다. token 갱신은 외부
  secret 관리/운영 계층에서 처리해야 합니다.
- Agent runner는 Docker 컨테이너를 티켓별 1개 생성하고 종료 후 강제 삭제합니다.
- Anthropic, GitHub, GitLab, Slack, Jira, Linear 관련 secret은 로그 저장 전
  redaction 대상입니다.
- 운영에서는 agent image의 네트워크/볼륨 권한과 Docker socket 접근을 별도
  격리 계층으로 제한하세요.
