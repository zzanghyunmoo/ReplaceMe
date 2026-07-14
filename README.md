# ReplaceMe / DevAutomation

.NET 9 기반 개발 자동화 오케스트레이션 서버입니다. 티켓을 API로 입력하면
Kafka 큐를 통해 Docker 컨테이너에서 코딩 에이전트를 실행하고, 민감 작업은 MCP
approval tool을 통해 승인 결정을 받은 뒤 계속 진행합니다.

> **운영 경계:** 현재 HTTP API에는 인증/인가가 없고 API/worker가 host Docker
> socket을 사용합니다. 이 구성은 trusted single-user local development 전용이며,
> 외부·공유·production-like 환경에 그대로 노출하면 안 됩니다.

## 구성

- `src/DevAutomation.Api` — ASP.NET Core Minimal API, Kafka producer,
  Slack interactivity webhook, API OpenTelemetry 설정
- `src/DevAutomation.Worker` — Kafka agent job consumer와 AgentJob 실행 host
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
docker compose up --build api worker postgres kafka
```

OpenTelemetry trace/metric을 로컬에서 볼 때는 telemetry를 켜고 `observability`
profile을 추가합니다. 기본 stack은 collector 없이 동작합니다.

```bash
DEVAUTOMATION_Telemetry__Enabled=true \
  docker compose --profile observability up --build
```

Jaeger UI는 `http://localhost:16686`, Prometheus는 `http://localhost:9090`에서
확인합니다.

API는 `http://localhost:8080`에서 실행됩니다. Compose의 host port는
`127.0.0.1`에 bind됩니다. 전체 API를 proxy/tunnel로 공개하지 않습니다. Slack
callback QA만 `/api/slack/interactivity` 단일 경로를 제한한 proxy를 사용합니다.
Compose의 one-shot `migrate` 서비스가 EF Core migration을 먼저 적용한 뒤 `api`와
`worker`가 시작됩니다.
`worker` 서비스가 Kafka를 consume하고 agent job을 실행하며, API service는 HTTP
endpoint와 health check를 제공합니다. Compose의 `kafka` 서비스는 Redpanda를
Kafka-compatible broker로 실행하므로 애플리케이션 설정은 기존 `kafka:9092`
bootstrap server를 그대로 사용합니다. 에이전트 컨테이너는 같은 Docker 네트워크
(`devautomation-network`)에 붙어 승인 MCP 서버가 PostgreSQL과 notifier 설정을
사용할 수 있게 구성됩니다.

내부 HTTPS proxy 때문에 Docker build에서 NuGet/npm 인증서 오류가 나면 로컬 CA
인증서(`*.crt`)를 `docker/certs/`에 넣으세요. 이 파일들은 git에는 ignore되지만
Docker build 시 image trust store에 추가됩니다.

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

Run Passport endpoint는 `run-passport-summary/v1` ticket-scoped summary를
반환합니다. `runPassportId`는 attempt/rerun ID가 아니며 `runPassportUrl`은 configured
API base URL과 결합해야 하는 상대 경로입니다. 이 no-auth endpoint를 public
proxy/tunnel로 노출하지 않습니다.

## 설정

모든 민감값은 `appsettings.json`이 아니라 환경변수 또는 `.env`로 주입합니다.

- `DEVAUTOMATION_Queue__KafkaBootstrapServers`
- `DEVAUTOMATION_Queue__KafkaConsumerGroupId` — 기본 `devautomation-api`
  (기존 offset 호환용)
- `DEVAUTOMATION_Agent__RemoteRepositoryProvider` — `GitHub` 또는 `GitLab`
- `DEVAUTOMATION_Agent__GitHubToken`
- `DEVAUTOMATION_Agent__GitLabToken`
- `DEVAUTOMATION_Notifier__Provider` — `Slack`, `Gmail`, `None`
- `DEVAUTOMATION_IssueTracker__Provider` — `Jira`, `Linear`, `None`
- `DEVAUTOMATION_DocumentTool__Provider` — `Notion`, `Confluence`, `None`
- `DEVAUTOMATION_Telemetry__Enabled`
- `DEVAUTOMATION_Telemetry__OtlpEndpoint` — 로컬 compose profile 기본값은
  `http://otel-collector:4317`
- `DEVAUTOMATION_Telemetry__OtlpHeaders`

자세한 값은 `.env.example`을 참고하세요.

## 검증

```bash
dotnet restore DevAutomation.sln
dotnet build DevAutomation.sln
dotnet test DevAutomation.sln
```

로컬에 .NET 9 runtime이 없으면 Docker SDK 이미지로 테스트할 수 있습니다.

<!-- markdownlint-disable MD013 -->
```bash
docker run --rm -v "$PWD":/src -w /src \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  sh -lc 'dotnet restore DevAutomation.sln && dotnet build DevAutomation.sln --no-restore && dotnet test DevAutomation.sln --no-build'
```
<!-- markdownlint-enable MD013 -->

> Note: .NET 9는 STS release라 지원 기간이 짧습니다. 장기 운영 전에 .NET 10 LTS
> 전환 여부를 다시 확인하세요.

## 보안 메모

- Slack interactivity는 `X-Slack-Signature`와 `X-Slack-Request-Timestamp`를
  검증합니다.
- Gmail notifier는 Gmail API access token을 사용합니다. token 갱신은 외부
  secret 관리/운영 계층에서 처리해야 합니다.
- Agent runner는 Docker 컨테이너를 티켓별 1개 생성하고 종료 후 강제 삭제합니다.
- Anthropic, GitHub, GitLab, Slack, Jira, Linear 관련 secret은 agent 실행 로그
  저장 전 redaction 대상입니다. 모든 framework/provider/file log에 대한 전역 보장은
  아니므로 별도 secret scan이 필요합니다.
- 현재 broker에는 persistent volume이 없어 `docker compose down` 뒤 queue, DLQ,
  consumer offset이 유실될 수 있습니다. 일시 정지는 `stop/start`를 사용합니다.
- 운영에서는 agent image의 네트워크/볼륨/리소스 권한과 Docker socket 접근을 별도
  격리 계층으로 제한하고 API 인증/인가를 추가해야 합니다.
