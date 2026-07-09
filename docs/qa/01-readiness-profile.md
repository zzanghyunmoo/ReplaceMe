# QA 01. personal-github-linear-notion Readiness Profile

<!-- markdownlint-disable MD013 -->

## 목적

`personal-github-linear-notion` profile이 실행 전에 GitHub, Linear, Notion, Docker,
Kafka, PostgreSQL, agent image, secret redaction 준비 상태를 올바르게 판정하는지
확인합니다.

## 사전 준비

먼저 [`00-local-runbook.md`](./00-local-runbook.md)를 통과합니다.

```bash
export BASE_URL=http://localhost:8080
```

L3 실제 provider 테스트를 하려면 `.env`에 아래 값을 채우고 API를 재시작합니다.

```env
DEVAUTOMATION_Agent__RemoteRepositoryProvider=GitHub
DEVAUTOMATION_Agent__GitHubToken=<repo read/write 가능한 token>
DEVAUTOMATION_ProfileReadiness__GitHub__RepositoryUrl=https://github.com/<owner>/<repo>.git

DEVAUTOMATION_Linear__ApiKey=<Linear API key>
DEVAUTOMATION_ProfileReadiness__Linear__TeamId=<Linear team id>
DEVAUTOMATION_ProfileReadiness__Linear__ProjectId=<Linear project id>
DEVAUTOMATION_ProfileReadiness__Linear__ReadinessIssueId=<Linear issue id 또는 UUID>

DEVAUTOMATION_Notion__ApiToken=<Notion integration token>
DEVAUTOMATION_ProfileReadiness__Notion__SetupPageId=<Notion setup page id>
```

pre-run gate까지 확인하려면 다음도 설정합니다.

```env
DEVAUTOMATION_ProfileReadiness__SelectedProfile=personal-github-linear-notion
```

주의: `POST /doctor`와 failed pre-run gate는 설정된 Linear/Notion target에 실제 report를
쓸 수 있습니다. QA용 issue/page를 사용하는 것이 안전합니다.

## Sandbox preflight

L3 provider 테스트 전에 실제 쓰기 대상이 모두 QA용인지 먼저 기록합니다.

| 대상 | QA 값 | 확인 |
| --- | --- | --- |
| GitHub repository | `https://github.com/<owner>/<scratch-repo>.git` | 중요 repo가 아닌 disposable repo인지 확인 |
| Linear issue | `<readiness issue>` | QA comment를 남겨도 되는 issue인지 확인 |
| Notion setup page | `<setup page id>` | readiness section을 덮어써도 되는 page인지 확인 |
| Slack channel | `<channel id>` | 테스트 메시지를 보내도 되는 channel인지 확인 |

테스트 후 cleanup:

- GitHub: QA branch/PR 삭제 또는 close
- Linear: readiness QA comment 정리
- Notion: readiness QA section 확인/정리
- Slack: 테스트 메시지 확인 후 필요하면 삭제

## READY-001. 알 수 없는 profile은 runnable이 아니다

```bash
curl -s "$BASE_URL/api/readiness/profiles/not-a-profile" | jq .
```

기대 결과:

- HTTP 200입니다.
- `isRunnable`은 `false`입니다.
- `checks[].id`에 `profile.identity`가 있습니다.
- `repairHint`는 `personal-github-linear-notion` 사용을 안내합니다.

## READY-002. Inspect GET은 외부에 쓰지 않는다

```bash
curl -s "$BASE_URL/api/readiness/profiles/personal-github-linear-notion" | jq .
```

기대 결과:

- 응답에 `profileName`, `mode`, `isRunnable`, `checks`, `reportSurfaceResults`가 있습니다.
- `mode`는 `Inspect`입니다.
- Linear comment나 Notion page가 새로 생기지 않습니다.
- provider 설정이 비어 있으면 required check가 실패하고 `isRunnable=false`가 됩니다.

체크 포인트:

```bash
curl -s "$BASE_URL/api/readiness/profiles/personal-github-linear-notion" \
  | jq '.isRunnable, [.checks[] | {id, status, severity, repairHint}]'
```

## READY-003. 필수 설정 누락은 fail-closed로 보인다

`.env`에서 GitHub/Linear/Notion 값을 일부러 비운 상태로 API를 재시작한 뒤 실행합니다.

```bash
curl -s "$BASE_URL/api/readiness/profiles/personal-github-linear-notion" | jq .
```

기대 결과:

- `isRunnable=false`
- 누락된 설정이 있는 check는 `status=Failed`, `severity=Required`
- `summary` 또는 `repairHint`가 어떤 환경변수를 채워야 하는지 알려줍니다.

## READY-004. GitHub repo read/write 권한 확인

GitHub token이 대상 repo를 읽고 push할 수 있는지 확인합니다.

```bash
curl -s "$BASE_URL/api/readiness/profiles/personal-github-linear-notion" \
  | jq '.checks[] | select(.id == "github.repo.access")'
```

기대 결과:

- repo read + push permission이 있으면 `status=Passed`입니다.
- read-only token이면 `status=Failed`이고 push permission 확인 실패를 설명합니다.
- 응답에 token 원문이 노출되지 않습니다.

## READY-005. agent image 내부 `git`/`gh` capability 확인

```bash
curl -s "$BASE_URL/api/readiness/profiles/personal-github-linear-notion" \
  | jq '.checks[] | select(.id == "github.agent.gh.capability")'
```

기대 결과:

- `devautomation-claude:latest` image 안에 `git`과 `gh`가 있으면 `Passed`입니다.
- 이 probe는 token을 container에 주입하지 않고, network 없이 command 존재만 확인합니다.
- image가 없거나 명령이 없으면 `Failed`입니다.

## READY-006. Doctor mode는 Linear/Notion report를 남긴다

QA용 Linear issue와 Notion setup page를 지정한 뒤 실행합니다.

```bash
curl -s -X POST "$BASE_URL/api/readiness/profiles/personal-github-linear-notion/doctor" | jq .
```

기대 결과:

- `mode`는 `Doctor`입니다.
- `reportSurfaceResults`에 `Linear`, `Notion` 결과가 있습니다.
- 성공하면 QA용 Linear issue에 readiness comment가 생깁니다.
- 성공하면 QA용 Notion setup page에 readiness section이 create/update됩니다.
- publisher 실패가 required이면 `isRunnable=false`입니다.

## READY-007. Pre-run gate가 ticket 생성을 막는다

`.env`에 다음을 설정하고 API를 재시작합니다.

```env
DEVAUTOMATION_ProfileReadiness__SelectedProfile=personal-github-linear-notion
```

일부러 required check가 실패하도록 Notion/GitHub/Linear 값 중 하나를 비운 상태에서
티켓을 생성하기 전후의 DB row 수와 Kafka topic offset을 함께 기록합니다.

```bash
export READY_BLOCK_TITLE="QA readiness gate should block"

kafka_offset() {
  docker compose exec -T kafka rpk topic describe devautomation.agent-jobs \
    --print-partitions -X brokers=kafka:9092 2>/dev/null \
    | awk 'NR > 1 && $1 ~ /^[0-9]+$/ {sum += $6} END {print sum + 0}'
}

export READY_BEFORE_COUNT=$(docker compose exec -T postgres psql -U devautomation -d devautomation -tAc \
  "select count(*) from tickets where \"Title\" = '$READY_BLOCK_TITLE';")
export READY_BEFORE_OFFSET=$(kafka_offset)

curl -i -s -X POST "$BASE_URL/api/tickets" \
  -H 'Content-Type: application/json' \
  -d "{
    \"title\": \"$READY_BLOCK_TITLE\",
    \"description\": \"This ticket must not be created when readiness is not runnable.\",
    \"repoUrl\": \"https://github.com/example/repo.git\",
    \"baseBranch\": \"main\"
  }"

export READY_AFTER_COUNT=$(docker compose exec -T postgres psql -U devautomation -d devautomation -tAc \
  "select count(*) from tickets where \"Title\" = '$READY_BLOCK_TITLE';")
export READY_AFTER_OFFSET=$(kafka_offset)

echo "ticket count: $READY_BEFORE_COUNT -> $READY_AFTER_COUNT"
echo "kafka offset: $READY_BEFORE_OFFSET -> $READY_AFTER_OFFSET"
```

기대 결과:

- HTTP status는 `409 Conflict`입니다.
- 응답 title은 `Readiness gate blocked ticket creation`입니다.
- 응답 extension에 readiness report가 들어 있습니다.
- `ticket count`가 증가하지 않습니다.
- `kafka offset`이 증가하지 않습니다.

## READY-008. Secret redaction gap은 warning으로 보인다

Notion token 등 secret catalog에 아직 포함되지 않은 값이 있으면 redaction coverage check가
warning을 낼 수 있습니다.

```bash
curl -s "$BASE_URL/api/readiness/profiles/personal-github-linear-notion" \
  | jq '.checks[] | select(.id == "secrets.redaction.coverage")'
```

기대 결과:

- 기본 severity는 `Warning`입니다.
- warning-only 상태는 `isRunnable`을 막지 않습니다.
- summary/repair hint에 secret 원문이 노출되지 않습니다.

## 완료 체크리스트

- [ ] unknown profile이 `isRunnable=false`로 응답
- [ ] GET inspect가 Linear/Notion에 쓰지 않음
- [ ] 필수 설정 누락 시 required failure와 repair hint 확인
- [ ] GitHub repo read + push permission 확인
- [ ] agent image 내부 `git`/`gh` 확인
- [ ] POST doctor가 Linear/Notion QA target에 report 작성
- [ ] pre-run gate가 불완전한 환경에서 `409`로 ticket 생성을 차단
- [ ] secret 값이 응답/로그에 원문으로 노출되지 않음

<!-- markdownlint-enable MD013 -->
