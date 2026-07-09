# QA 00. 로컬 실행과 Health Check

<!-- markdownlint-disable MD013 -->

## 목적

ReplaceMe API, PostgreSQL, Kafka-compatible broker, Docker agent image가 로컬에서
실행 가능한지 확인합니다. Compose의 `kafka` 서비스는 Redpanda를 실행합니다. 이 문서를
통과해야 다른 기능별 QA를 안정적으로 진행할 수 있습니다.

## 사전 준비

| 항목 | 필요 여부 | 확인 명령 |
| --- | --- | --- |
| Docker Desktop 또는 Docker Engine | 필수 | `docker version` |
| Docker Compose v2 | 필수 | `docker compose version` |
| .NET SDK | 권장 | `dotnet --info` |
| .NET 8 runtime | host test 실행 시 필요 | `dotnet --list-runtimes` |
| `jq` | 선택 | `jq --version` |

로컬 host에 .NET 8 runtime이 없으면 Docker SDK 8 이미지로 test를 실행하면 됩니다.

## 기본 환경 준비

```bash
cd /Users/gurumee92/Workspaces/zWorkspaces/projects/ReplaceMe
cp .env.example .env
export BASE_URL=http://localhost:8080
```

처음에는 외부 provider를 끄고 시작합니다.

```env
DEVAUTOMATION_Notifier__Provider=None
DEVAUTOMATION_IssueTracker__Provider=None
DEVAUTOMATION_DocumentTool__Provider=None
DEVAUTOMATION_ProfileReadiness__SelectedProfile=
```

## LOCAL-001. agent image build

```bash
docker compose --profile build-only build agent-image
```

기대 결과:

- `devautomation-claude:latest` image build가 성공합니다.
- 실패하면 `Dockerfile.agent`와 네트워크/NuGet/npm 접근을 먼저 확인합니다.

확인 명령:

```bash
docker image inspect devautomation-claude:latest >/dev/null && echo "agent image exists"
```

## LOCAL-002. API/PostgreSQL/Redpanda 실행

```bash
docker compose up --build api postgres kafka
```

기대 결과:

- `postgres`가 healthy 상태가 됩니다.
- `kafka` 서비스가 Redpanda Kafka-compatible broker로 시작됩니다.
- `api`가 `0.0.0.0:8080`에 바인딩됩니다.
- API 시작 시 EF Core migration이 적용됩니다.

다른 터미널에서 확인합니다.

```bash
docker compose ps
```

## LOCAL-003. `/health` 확인

```bash
curl -s "$BASE_URL/health" | jq .
```

기대 응답:

```json
{
  "db": "ok",
  "kafka": "ok",
  "docker": "ok"
}
```

판정:

| 결과 | 의미 | 다음 조치 |
| --- | --- | --- |
| HTTP 200 + 모두 `ok` | 로컬 인프라 준비 완료 | 다음 QA로 이동 |
| `db` failed | PostgreSQL 연결 실패 | `docker compose ps postgres`, `docker compose logs postgres` 확인 |
| `kafka` failed | Kafka API metadata 조회 실패 | `docker compose logs kafka` 확인 |
| `docker` failed | API container에서 Docker daemon 접근 실패 | Docker socket volume mount 확인 |

## LOCAL-004. build/test 검증

host에 .NET 8 SDK/runtime이 있으면 다음을 실행합니다.

```bash
dotnet restore DevAutomation.sln
dotnet build DevAutomation.sln
dotnet test DevAutomation.sln
```

host에 .NET 8 SDK/runtime이 없으면 restore, build, test를 모두 Docker SDK 8 안에서
실행합니다. 이 경로에서는 host `dotnet` 명령을 사용하지 않습니다.

```bash
docker run --rm -v "$PWD":/src -w /src \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  sh -lc 'dotnet restore DevAutomation.sln && dotnet build DevAutomation.sln --no-restore && dotnet test DevAutomation.sln --no-build'
```

기대 결과:

- build warning/error 없이 성공합니다.
- test가 모두 통과합니다.
- host `dotnet test`가 .NET 8 runtime 부재로 실패하면 Docker SDK 8 결과를 기준으로
  기록합니다.

## LOCAL-005. DB migration 확인

```bash
docker compose exec postgres \
  psql -U devautomation -d devautomation -c '\dt'
```

기대 결과:

- `tickets`, `approval_requests`, `execution_logs` 또는 EF Core migration history table이 보입니다.

## LOCAL-006. 파일 로그 확인

```bash
ls -la logs
find logs -type f -maxdepth 1 -print
```

기대 결과:

- API가 요청을 처리하면 `logs/devautomation-*.log` 파일이 생깁니다.
- Docker Compose가 `./logs:/app/logs` volume을 연결하므로 host에서 확인 가능합니다.

## 실패 시 빠른 복구

```bash
# 컨테이너만 재시작
docker compose down
docker compose up --build api postgres kafka

# DB와 broker volume까지 초기화
docker compose down -v
docker compose up --build api postgres kafka
```

## 완료 체크리스트

- [ ] `agent-image` build 성공
- [ ] `docker compose ps`에서 API/PostgreSQL/Redpanda 실행 확인
- [ ] `/health`가 `db/kafka/docker = ok` 반환
- [ ] `dotnet build` 성공
- [ ] host 또는 Docker SDK 8에서 `dotnet test` 성공
- [ ] DB table 확인
- [ ] 파일 로그 생성 확인

<!-- markdownlint-enable MD013 -->
