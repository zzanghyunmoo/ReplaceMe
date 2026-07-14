# 로컬 실행과 운영 확인

## 무엇을 하는 기능인가

ReplaceMe는 Docker Compose로 API, worker, PostgreSQL, Kafka-compatible broker,
agent image를 함께 실행할 수 있게 구성되어 있습니다. Compose의 `api` 서비스는
HTTP endpoint와 health check만 담당하고, `worker` 서비스가 Kafka agent job을
consume합니다. Compose의 `kafka` 서비스는 Redpanda를 실행하며, 애플리케이션은
Kafka API로 접근합니다. 선택 사항인 `observability` profile은 OpenTelemetry
Collector, Jaeger, Prometheus를 추가해 API/worker trace와 metric을 로컬에서
확인하게 합니다. `/health` endpoint로 PostgreSQL, broker, Docker daemon 연결 상태를
확인합니다.

> 현재 Compose는 host port를 `127.0.0.1`에 bind하지만 인증 없는 API와 host
> Docker socket을 사용합니다. trusted single-user local development 전용이며 전체
> API를 proxy/tunnel로 공개하지 않습니다. Slack callback QA만 단일 path 제한을 둔
> proxy를 사용합니다.

## 한눈에 보기

<!-- markdownlint-disable MD013 -->
| 항목 | 내용 |
| --- | --- |
| 시작 조건 | `.env`를 준비하고 Docker Compose를 실행합니다. |
| 핵심 책임 | 로컬 API, worker, DB, Kafka-compatible broker, agent image를 함께 띄웁니다. |
| 선택 profile | `observability`는 OTel Collector, Jaeger, Prometheus를 추가합니다. |
| 주요 확인 | `/health`, compose container 상태, test 명령입니다. |
| 실패 시 | DB/broker/Docker 연결 문제를 먼저 확인합니다. |
| Readiness | `/health`와 별도로 profile readiness endpoint로 provider/secret/agent posture를 확인합니다. |
<!-- markdownlint-enable MD013 -->

## 실행 구성

```mermaid
flowchart LR
    Compose[docker-compose.yml] --> Migrate[migrate\nEF Core migrations]
    Compose --> API[api\nASP.NET Core HTTP]
    Compose --> Worker[worker\nKafkaAgentWorker]
    Compose --> Postgres[(postgres\nticket / approval / logs)]
    Compose --> Kafka[(kafka service\nRedpanda broker)]
    Compose --> AgentImage[agent-image\nClaude Code + Approval MCP]
    Compose -. observability profile .-> Collector[otel-collector]
    Collector -. traces .-> Jaeger[Jaeger UI]
    Collector -. metrics .-> Prometheus[Prometheus]
    Migrate --> Postgres
    API --> Postgres
    API --> Kafka
    Worker --> Postgres
    Worker --> Kafka
    Worker --> DockerSock[/Docker socket/]
    API -. OTLP when enabled .-> Collector
    Worker -. OTLP when enabled .-> Collector
    DockerSock --> AgentImage
```

## 빠른 실행

```bash
cp .env.example .env
# .env에 Anthropic/GitHub 또는 GitLab, notifier, issue tracker, document tool 값을 입력

docker compose --profile build-only build agent-image
docker compose up --build api worker postgres kafka
```

관측성 stack을 함께 띄울 때는 telemetry를 켠 뒤 `observability` profile을 사용합니다.
기본 stack에서는 telemetry가 꺼져 있어 collector가 필요하지 않습니다.

```bash
DEVAUTOMATION_Telemetry__Enabled=true \
  docker compose --profile observability up --build
```

로컬 UI와 endpoint는 다음 주소를 사용합니다.

| 대상 | 주소 | 용도 |
| --- | --- | --- |
| Jaeger | `http://localhost:16686` | API/worker trace 조회 |
| Prometheus | `http://localhost:9090` | Collector가 받은 metric 조회 |
| OTel Collector gRPC | `http://localhost:4317` | OTLP/gRPC 수신 |
| OTel Collector HTTP | `http://localhost:4318` | OTLP/HTTP 수신 |

`api`와 `worker`는 `migrate` one-shot service가 EF Core migration을 끝낸 뒤
시작합니다. `docker compose up --build api worker postgres kafka`를 실행하면
`migrate`는 dependency로 자동 실행됩니다.

API는 기본적으로 다음 주소에서 열립니다.

```text
http://localhost:8080
```

Compose의 `kafka` 서비스는 Redpanda를 사용합니다. compose 내부에서는
`kafka:9092`, 호스트에서 직접 실행하는 API/도구에서는 `localhost:9092`로 접근할
수 있게 Kafka API listener가 구성되어 있습니다.

Docker build가 내부 HTTPS proxy 뒤에서 NuGet/npm 인증서 오류를 만나면 로컬 CA
인증서(`*.crt`)를 `docker/certs/`에 둘 수 있습니다. `.crt` 파일은 git에는 ignore되며,
Dockerfiles가 build 시 image trust store에 추가합니다.

## 환경변수

<!-- markdownlint-disable MD013 -->
| 환경변수 | 설명 |
| --- | --- |
| `DEVAUTOMATION_Queue__KafkaBootstrapServers` | API/worker가 사용할 Kafka API broker |
| `DEVAUTOMATION_Queue__KafkaConsumerGroupId` | worker consumer group, 기본 `devautomation-api`(기존 offset 호환용) |
| `DEVAUTOMATION_Queue__KafkaDlqTopic` | exhausted/poison agent job을 publish할 DLQ topic |
| `DEVAUTOMATION_Queue__MaxAttempts` | worker 처리 실패를 DLQ 전에 시도할 최대 Kafka attempt 수 |
| `DEVAUTOMATION_Agent__AnthropicApiKey` | agent container에 주입할 Anthropic API key |
| `DEVAUTOMATION_Agent__RemoteRepositoryProvider` | `GitHub` 또는 `GitLab` |
| `DEVAUTOMATION_Agent__GitHubToken` | GitHub push/PR 생성 token |
| `DEVAUTOMATION_Agent__GitLabToken` | GitLab push/MR 생성 token |
| `DEVAUTOMATION_Agent__DockerNetwork` | agent container가 붙을 Docker network |
| `DEVAUTOMATION_Agent__ExecutionIsolationProfile` | local은 `LocalDevelopment`, 공유 환경은 `ProductionLike` |
| `DEVAUTOMATION_Agent__DockerSocketMode` | local Compose runner는 `LocalDockerSocket` |
| `DEVAUTOMATION_Agent__AllowLocalDockerSocket` | host Docker socket 사용 명시적 opt-in |
| `DEVAUTOMATION_Agent__AllowLocalDockerSocketInProductionLike` | production-like 예외 opt-in |
| `DEVAUTOMATION_Langfuse__SecretKey` | 선택적 AI observability secret, 기본 비활성 |
| `DEVAUTOMATION_LiteLLM__ApiKey` | 선택적 gateway admin/proxy secret, 기본 비활성 |
| `DEVAUTOMATION_LiteLLM__VirtualKey` | 선택적 gateway virtual key, 기본 비활성 |
| `DEVAUTOMATION_Notifier__Provider` | `Slack`, `Gmail`, `None` |
| `DEVAUTOMATION_IssueTracker__Provider` | `Jira`, `Linear`, `None` |
| `DEVAUTOMATION_DocumentTool__Provider` | `Notion`, `Confluence`, `None` |
| `DEVAUTOMATION_Telemetry__Enabled` | OpenTelemetry export 활성화 여부 |
| `DEVAUTOMATION_Telemetry__OtlpEndpoint` | OTLP endpoint, Compose 관측성 profile 기본값은 `http://otel-collector:4317` |
| `DEVAUTOMATION_Telemetry__OtlpHeaders` | 외부 collector/SaaS 사용 시 추가 OTLP header, 로컬 profile은 비워 둡니다. |
<!-- markdownlint-enable MD013 -->

`appsettings.json`에는 민감값을 넣지 않고, `.env` 또는 runtime environment로
주입합니다. 전체 예시는 `.env.example`을 참고하세요. Retry/DLQ와 Docker isolation
posture는 Compose `${VAR:-default}` interpolation을 사용하므로 값을 바꾼 뒤 API/worker를
recreate합니다. Langfuse/LiteLLM 값은 향후 AI observability/gateway 통합을 위한
placeholder이며 core local stack에서는 비워 둡니다.

## Local-only Docker socket safety

`api`와 `worker`는 local Compose에서 host Docker socket을 mount합니다. 이 구성은
agent container 생성과 Docker readiness를 위한 local-only 편의 기능입니다.
공유 또는 production-like 환경에서는 `DEVAUTOMATION_Agent__ExecutionIsolationProfile=ProductionLike`를
설정해야 하며, local socket 예외 opt-in이 없으면 readiness가 required failure를
반환합니다. 예외 opt-in을 켜더라도 임시 break-glass로 기록하고, host socket 없는 격리
runner로 이전해야 합니다.

ZZA-62 observability profile이나 향후 Langfuse/LiteLLM profile을 켜도 이 원칙은 변하지
않습니다. observability/gateway secret은 redaction catalog에 포함되지만, local stack은
기본적으로 해당 서비스를 요구하거나 agent container에 해당 secret을 전달하지 않습니다.

## Health check

`GET /health`는 다음 dependency를 확인합니다.

```mermaid
flowchart TD
    H[GET /health] --> DB{PostgreSQL 연결?}
    H --> K{Kafka metadata 조회?}
    H --> D{Docker daemon ping?}
    DB -- ok --> OK[200 OK 후보]
    K -- ok --> OK
    D -- ok --> OK
    DB -- failed --> P[Problem response]
    K -- failed --> P
    D -- failed --> P
```

모두 정상이면 `200 OK`, 하나라도 실패하면 `Problem` 응답을 반환합니다.

`/health`는 서비스 dependency 확인용입니다. GitHub, Linear, Notion 권한까지
확인하는 기능은 `personal-github-linear-notion` readiness profile endpoint에서 따로
확인합니다.

## Readiness profile 확인

`personal-github-linear-notion` profile은 agent run 전에 GitHub, Linear, Notion,
Docker, Kafka, PostgreSQL, agent image, secret redaction 준비 상태를 확인합니다.

```bash
# 조회 전용: Linear/Notion에 쓰지 않음
curl http://localhost:8080/api/readiness/profiles/personal-github-linear-notion

# 수동 doctor: 설정된 경우 Linear/Notion에 readiness report를 남김
curl -X POST http://localhost:8080/api/readiness/profiles/personal-github-linear-notion/doctor
```

`DEVAUTOMATION_ProfileReadiness__SelectedProfile=personal-github-linear-notion`을
설정하면 `/api/tickets`와 `AgentJob.RunAsync` 앞에서 pre-run gate가 동작합니다.
required check가 실패하면 ticket 생성은 `409 ProblemDetails`로 막히고, 이미 queued
된 ticket은 `Failed` 상태와 `Readiness gate blocked:` 사유를 남깁니다.

## 중지와 데이터 보존

현재 PostgreSQL에는 named volume이 있지만 Redpanda에는 volume이 없습니다.

```bash
# DB와 queue/DLQ/consumer offset을 유지
docker compose stop
docker compose start

# PostgreSQL volume은 남지만 broker state는 유실될 수 있음
docker compose down

# PostgreSQL까지 삭제: disposable QA에서만
docker compose down -v
```

따라서 `down` 전에 Pending ticket과 DLQ를 확인합니다. DB row만 남고 Kafka message가
사라지면 자동 복구하는 outbox/reconciler는 아직 없습니다.

## 개발 검증 명령

```bash
dotnet restore DevAutomation.sln
dotnet build DevAutomation.sln
dotnet test DevAutomation.sln
```

기대 결과:

1. restore가 NuGet package를 정상 복원합니다.
2. build가 compile error 없이 끝납니다.
3. test가 domain/service/infrastructure test를 통과합니다.
4. `/health`는 DB/Kafka-compatible broker/Docker가 준비된 로컬 환경에서 `200 OK`를 반환합니다.

로컬 머신에 .NET 9 runtime이 없다면 Docker SDK 이미지로 테스트할 수 있습니다.

```bash
docker run --rm -v "$PWD":/src -w /src \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  bash -lc 'dotnet restore DevAutomation.sln && \
    dotnet build DevAutomation.sln --no-restore && \
    dotnet test DevAutomation.sln --no-build'
```

.NET 9는 STS release라 지원 기간이 짧습니다. 장기 운영 전에 .NET 10 LTS 전환
여부를 다시 확인하세요.

## 코드 위치

- Compose: `docker-compose.yml`
- OTel Collector config: `docker/otel-collector-config.yaml`
- Prometheus scrape config: `docker/prometheus.yml`
- API/worker image targets: `Dockerfile`
- Agent image: `Dockerfile.agent`
- 설정: `src/DevAutomation.Api/appsettings.json`, `.env.example`
- API endpoints and health endpoint: `src/DevAutomation.Api/Program.cs`
- Worker host: `src/DevAutomation.Worker/Program.cs`
- Kafka producer/consumer: `src/DevAutomation.Infrastructure/Queues/`

## 현재 한계

- production deployment manifest는 아직 없습니다.
- Docker socket mount는 local-only로 명시되어 있으며 readiness와 runner guard에서
  warning/block을 제공합니다. 운영 등급 runner 격리는 후속 작업입니다.
- Kafka worker는 처리 예외를 bounded retry 후 DLQ로 보내지만, DLQ replay tooling은 아직 없습니다.
- API 인증/인가, protected ingress, worker health endpoint, persistent broker가 없습니다.
