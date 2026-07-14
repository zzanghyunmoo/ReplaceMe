# Run Passport v1 티켓 요약 계약

## 무엇을 하는 기능인가

Run Passport v1은 하나의 `Ticket` workflow에서 현재 상태, 안전하게 공개할 수 있는
외부 링크, lifecycle timestamp를 읽는 공통 요약 계약입니다.

`runPassportId = ticket:{ticketId}`는 Kafka attempt나 rerun마다 새로 생기는 immutable
run ID가 아닙니다. Retry가 발생해도 같은 Ticket과 Passport ID를 사용하며 응답은 최신
Ticket 상태에 따라 바뀝니다.

```http
GET /api/tickets/{id}/run-passport
```

이 endpoint는 읽기 전용입니다. 기존 Ticket이면 `200 OK`, 없으면 `404 Not Found`를
반환하며 Ticket 상태, execution log, Kafka queue를 변경하지 않습니다.

> API에는 인증/인가가 없습니다. Compose host port의 loopback 강제는 stacked
> dependency인 PR #22가 소유하며 이 PR에는 해당 파일을 중복하지 않습니다. PR #22가
> 함께 적용되지 않은 배포에서는 이 endpoint를 public proxy/tunnel로 노출하지 않고
> trusted single-user local 환경에서만 사용합니다.

## 한눈에 보기

<!-- markdownlint-disable MD013 -->
| 항목 | 내용 |
| --- | --- |
| Contract version | `run-passport-summary/v1` |
| 핵심 책임 | Ticket에서 mutable workflow summary를 파생합니다. |
| 주요 출력 | `RunPassportSummaryResponse` |
| 저장 방식 | 별도 Run Passport table이 없는 derived response입니다. |
| Identity 경계 | Ticket-scoped이며 attempt/rerun identity가 아닙니다. |
| 후속 소비자 | Notion lifecycle, PR review packet, Linear handoff 후보 |
<!-- markdownlint-enable MD013 -->

## 응답 필드

<!-- markdownlint-disable MD013 -->
| 필드 | 의미 | v1 규칙 |
| --- | --- | --- |
| `contractVersion` | 계약 버전 | 상수 `run-passport-summary/v1` |
| `runPassportId` | Ticket workflow 식별자 | `ticket:{ticketId}`; retry에서도 유지 |
| `runPassportUrl` | summary 조회 상대 경로 | `/api/tickets/{id}/run-passport` |
| `ticketId`, `title` | 원본 Ticket 참조 | `Ticket.Id`, `Ticket.Title` |
| `status` | 현재 Ticket 상태 문자열 | `Pending`, `Running`, `WaitingApproval`, `Completed`, `Failed`, `Cancelled` |
| `summary` | machine-facing lifecycle 요약 | 상태와 검증된 PR URL 유무에서 파생 |
| `createdAt`, `startedAt`, `completedAt` | Ticket lifecycle timestamp | 시작/종료 전에는 nullable |
| `lastLifecycleAt` | 알려진 마지막 lifecycle timestamp | `CompletedAt ?? StartedAt ?? CreatedAt` |
| `issueTracker`, `externalIssueKey` | 연결된 issue provider/key | `Jira`, `Linear`, 또는 `null` |
| `externalIssueUrl` | 검증된 issue 링크 | 안전하지 않거나 허용 host가 아니면 `null` |
| `pullRequestUrl` | 검증된 PR/MR 링크 | 안전하지 않거나 repo host와 다르면 `null` |
| `notionDocumentId`, `notionDocumentUrl` | lifecycle 문서 참조 | 현재 `null`; ZZA-52 publication 소유 |
| `testSummary`, `residualRiskSummary` | 검증/리스크 evidence | 현재 `null`; future evidence/PR packet 소유 |
| `failureReason` | public-safe terminal detail | `Execution failed.` 또는 `Execution cancelled.`; raw reason 미노출 |
<!-- markdownlint-enable MD013 -->

`lastLifecycleAt`은 일반적인 row 수정 시각이나 synchronization cursor가 아닙니다.
Approval 대기 진입이나 retry 복귀처럼 `StartedAt`/`CompletedAt`을 바꾸지 않는 전이는
이 값에 반영되지 않을 수 있습니다.

`runPassportUrl`도 absolute external backlink가 아니라 상대 경로입니다. Consumer는
configured ReplaceMe API base URL과 결합한 뒤 사용해야 하며, unresolved path를 Notion
또는 PR 링크로 그대로 게시하지 않습니다.

## 상태별 summary

<!-- markdownlint-disable MD013 -->
| 상태 | 조건 | `summary` |
| --- | --- | --- |
| `Pending` | `startedAt == null` | `Ticket is pending and has not started agent execution.` |
| `Pending` | `startedAt != null` | `Ticket is pending retry after an earlier execution attempt.` |
| `Running` | 항상 | `Ticket is running agent execution.` |
| `WaitingApproval` | 항상 | `Ticket is waiting for approval.` |
| `Completed` | 검증된 PR/MR URL 있음 | `Ticket completed with a pull request.` |
| `Completed` | 검증된 PR/MR URL 없음 | `Ticket completed without a pull request.` |
| `Failed` | 항상 | `Execution failed.` |
| `Cancelled` | 항상 | `Execution cancelled.` |
<!-- markdownlint-enable MD013 -->

Failed/Cancelled 응답은 raw `Ticket.FailReason`을 반환하지 않습니다. Token assignment,
header, JSON credential, credential-bearing URL, Unix/Windows local path가 원본 reason에
있어도 API에는 generic public-safe 문장만 노출합니다.

## 외부 URL 검증

Issue/PR URL은 다음 조건을 모두 만족할 때만 반환합니다.

1. Absolute HTTPS URL입니다.
2. URL userinfo가 없습니다.
3. Query와 fragment를 percent-decode했을 때 `token`, `secret`, `password`,
   `credential`, `signature`, API/access key 계열의 credential assignment가 없습니다.
4. Provider별 허용 host와 일치합니다.

Host 규칙:

- Linear issue: `linear.app`
- Jira issue: `Jira:BaseUrl`의 host
- PR/MR: Ticket `repoUrl`의 host

빈 문자열, whitespace, HTTP, credential 포함 URL, 예상하지 않은 host는 `null`로
정규화됩니다. Query/fragment의 malformed percent encoding, decoded nested assignment
(`foo=token%3Dsecret`), JSON credential field, fragment assignment도 fail closed로
`null`이 됩니다. 정상적인 path와 credential assignment가 없는 query/fragment 값은
원문 그대로 유지합니다.

이 검증의 normative boundary는 URL authority와 query/fragment의 credential-key
assignment syntax입니다. Query/fragment는 안정될 때까지 최대 16회 percent-decode하며,
그 전에 malformed encoding이 나타나거나 제한 안에 안정되지 않으면 거부합니다. Path를
secret scanner처럼 해석하거나 URL 모든 위치의 임의 문자열이 secret이 아니라고
보증하지는 않습니다. 또한 PR/MR은 이 slice에서 `repoUrl`과 host만 비교합니다. 동일
host 안의 repository identity/index 검증은 stacked dependency인 PR #23이 소유하며 이
PR에는 해당 파일을 중복하지 않습니다. `repoUrl`과 `baseBranch` 자체는 Run Passport
응답 필드가 아니므로 필요한 downstream consumer는 별도 Ticket API에서 읽습니다.

## Nullable field availability

<!-- markdownlint-disable MD013 -->
| 필드 | Producer | `null` 의미 | 응답 차단 |
| --- | --- | --- | --- |
| `pullRequestUrl` | Agent/repository provider | 생성 안 됨, 비어 있음, 검증 실패 | 아니요 |
| `notionDocumentId`, `notionDocumentUrl` | ZZA-52 lifecycle publisher | 아직 publication 없음 | 아니요 |
| `testSummary` | Future evidence collector | 아직 수집 안 됨 | 아니요 |
| `residualRiskSummary` | ZZA-55/evidence collector | 아직 수집 안 됨 | 아니요 |
| `failureReason` | Public-safe terminal projector | 공개할 terminal detail 없음 | 아니요 |
<!-- markdownlint-enable MD013 -->

## 예시

```json
{
  "contractVersion": "run-passport-summary/v1",
  "runPassportId": "ticket:00000000-0000-0000-0000-000000000000",
  "runPassportUrl": "/api/tickets/00000000-0000-0000-0000-000000000000/run-passport",
  "ticketId": "00000000-0000-0000-0000-000000000000",
  "title": "Build feature",
  "status": "Completed",
  "summary": "Ticket completed with a pull request.",
  "createdAt": "2026-07-09T00:00:00+00:00",
  "startedAt": "2026-07-09T00:01:00+00:00",
  "completedAt": "2026-07-09T00:05:00+00:00",
  "lastLifecycleAt": "2026-07-09T00:05:00+00:00",
  "issueTracker": "Linear",
  "externalIssueKey": "ZZA-56",
  "externalIssueUrl": "https://linear.app/example/issue/ZZA-56",
  "pullRequestUrl": "https://github.com/example/repo/pull/56",
  "notionDocumentId": null,
  "notionDocumentUrl": null,
  "testSummary": null,
  "residualRiskSummary": null,
  "failureReason": null
}
```

## 코드와 검증 위치

- Contract/policy: `src/DevAutomation.Core/Contracts/RunPassportContracts.cs`
- API endpoint: `src/DevAutomation.Api/Program.cs`
- Domain source: `src/DevAutomation.Core/Entities/Ticket.cs`
- Contract tests: `tests/DevAutomation.Tests/RunPassportContractTests.cs`
- HTTP tests: `tests/DevAutomation.Tests/RunPassportEndpointTests.cs`

HTTP test는 `200`, `404`, camelCase v1 wire shape, explicit null,
Jira configured host, Ticket/log/Kafka non-mutation을 검증합니다.

## 현재 한계

- 별도 Run Passport persistence table이 없습니다.
- Immutable attempt identity, rerun lineage, replay 기록이 없습니다.
- `lastLifecycleAt`은 모든 Ticket mutation을 나타내지 않습니다.
- Notion lifecycle publication과 PR review packet/evidence producer는 설계만 완료됐고
  자동 구현은 아직 없습니다.
- `runPassportUrl`을 external absolute URL로 바꾸는 canonical public base URL 계약이
  없습니다.
- Compose loopback 강제는 stacked PR #22, repository identity/index 검증은 stacked
  PR #23에서 해결되며 이 PR은 해당 변경 파일을 중복하지 않습니다.
