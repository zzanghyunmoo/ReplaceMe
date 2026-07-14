# 운영자 가이드

Notion canonical:
[운영자 가이드](https://app.notion.com/p/398ef22ad4fc81d1860df0550831f981)

기준일: 2026-07-14

> **운영 등급:** trusted single-user local development only. API 인증/인가와
> production-grade runner 격리가 없으므로 외부·공유·운영 환경에 그대로 노출하지
> 않습니다.

## 1. 사전 점검

```bash
cd /path/to/ReplaceMe
docker version
docker compose version
dotnet --info
cp .env.example .env
chmod 600 .env
```

첫 smoke에서는 외부 쓰기를 끕니다.

```env
DEVAUTOMATION_Notifier__Provider=None
DEVAUTOMATION_IssueTracker__Provider=None
DEVAUTOMATION_DocumentTool__Provider=None
DEVAUTOMATION_ProfileReadiness__SelectedProfile=
DEVAUTOMATION_Telemetry__Enabled=false
```

## 2. 구성·빌드·시작

```bash
docker compose config --quiet
docker compose --profile observability config --quiet
docker compose --profile build-only build agent-image
docker image inspect devautomation-claude:latest >/dev/null
docker compose up -d --build api worker postgres kafka
docker compose ps -a
docker compose logs --no-color migrate
docker compose logs --no-color --tail=200 api worker postgres kafka
```

정상 기준:

- `migrate`: exit 0
- `postgres`: healthy
- `api`, `worker`, `kafka`: running
- worker: topic 생성/존재와 consumer 시작 로그

## 3. Health와 readiness

```bash
export BASE_URL=http://localhost:8080
curl -fsS "$BASE_URL/health" | jq .
docker compose exec -T kafka rpk topic list -X brokers=kafka:9092
docker compose logs --no-color --tail=200 worker
curl -fsS \
  "$BASE_URL/api/readiness/profiles/personal-github-linear-notion" | jq .
```

- `/health`: PostgreSQL, Kafka metadata, Docker ping만 확인합니다.
- readiness GET: provider, agent image, secret coverage, socket posture를 비파괴
  검사합니다.
- doctor POST: Linear/Notion에 실제 report를 쓸 수 있으므로 QA target에서만
  실행합니다.

## 4. 보안 경계

- Compose는 host port를 `127.0.0.1`에 bind합니다. 전체 API를 proxy/tunnel로
  공개하지 않습니다.
- Slack callback QA만 `/api/slack/interactivity`를 allowlist한 proxy를 사용합니다.
- API와 worker의 Docker socket mount는 host 지배 권한에 준합니다.
- Retry/DLQ와 isolation posture는 Compose interpolation을 사용합니다. `.env` 또는
  shell environment를 바꾼 뒤 API/worker를 recreate합니다.
- `.env`, token, raw prompt/output, approval payload, 개인 경로를 문서·PR·로그에
  남기지 않습니다.

## 5. 장애 조사

```bash
docker compose ps -a
docker compose logs --no-color --since=30m api worker migrate kafka
tail -n 200 logs/devautomation-*.log logs/devautomation-worker-*.log
docker compose exec -T kafka rpk topic consume \
  devautomation.agent-jobs.dlq -n 1 -X brokers=kafka:9092
docker ps -a --filter 'label=devautomation.ticket-id=<ticket-id>'
```

판정 순서:

1. migrate exit code와 migration history
2. `/health`의 db/kafka/docker
3. worker topic creation/consume/reconnect/retry/DLQ
4. Ticket `status`, `failReason`, `containerId`
5. `/api/tickets/{id}/logs`
6. DLQ source/partition/offset/attempt/failure
7. 남은 agent container

DLQ 자동 replay 도구는 없습니다. terminal 상태와 side effect를 검토하지 않고 main
queue에 재발행하지 않습니다.

## 6. 관측성

```bash
DEVAUTOMATION_Telemetry__Enabled=true \
  docker compose --profile observability up -d --build
```

- Jaeger: <http://localhost:16686>
- Prometheus: <http://localhost:9090>

현재 alert rule, Alertmanager, dashboard, SLO, backend persistence는 없습니다.

## 7. 중지·백업·초기화

```bash
# DB와 queue/DLQ/offset을 유지
docker compose stop
docker compose start

# PostgreSQL volume은 남지만 현재 Redpanda state는 유실될 수 있음
docker compose down

# PostgreSQL까지 삭제: disposable QA에서만
docker compose down -v
```

DB 변경 전 백업:

```bash
docker compose exec -T postgres pg_dump \
  -U devautomation -d devautomation > replaceme-backup-$(date +%Y%m%d-%H%M%S).sql
```

## 8. QA

상세 케이스는 [`../qa/README.md`](../qa/README.md)를 기준으로 실행합니다.

- L0: restore/build/test
- L1: Compose/migration/health
- L2: API/readiness/persistence/DLQ failure path
- L3: QA용 외부 provider/Slack
- L4: scratch repo full agent/PR

현재 자동 테스트 76개는 unit/composition/HTTP 계약을 증명하지만 실제 external provider와
Compose E2E 전체를 증명하지 않습니다.
