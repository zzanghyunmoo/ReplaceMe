# Run Passport 최소 계약

## 무엇을 하는 기능인가

Run Passport는 자동화 실행 한 번을 추적하기 위한 공통 요약 계약입니다. 이번 v0는
새 테이블이나 rerun lineage를 만들지 않고, 기존 `Ticket` 상태에서 읽을 수 있는 값만
`GET /api/tickets/{id}/run-passport`로 노출합니다.

이 계약은 ZZA-52 Notion lifecycle documents와 ZZA-55 PR review packet이 같은
필드명과 링크 의미를 사용하게 만드는 첫 번째 얇은 연결점입니다.

## 한눈에 보기

<!-- markdownlint-disable MD013 -->
| 항목 | 내용 |
| --- | --- |
| 시작 조건 | 티켓이 이미 생성되어 있습니다. |
| 핵심 책임 | 티켓에서 Run Passport summary v0를 파생해 반환합니다. |
| 주요 출력 | `RunPassportSummaryResponse` |
| 저장 방식 | v0에서는 별도 Run Passport table을 만들지 않습니다. |
| 후속 소비자 | Notion lifecycle docs, PR review packet, Linear execution handoff |
<!-- markdownlint-enable MD013 -->

## 엔드포인트

<!-- markdownlint-disable MD013 -->
| 메서드 | 경로 | 설명 |
| --- | --- | --- |
| `GET` | `/api/tickets/{id}/run-passport` | 티켓에서 파생한 Run Passport summary v0 반환 |
<!-- markdownlint-enable MD013 -->

티켓이 없으면 `404 Not Found`를 반환합니다. 티켓이 있으면 실행을 시작하거나 상태를
바꾸지 않고 읽기 전용 summary만 반환합니다.

## Contract version

현재 contract version은 다음 값입니다.

```text
run-passport-summary/v0
```

v0의 목적은 downstream surface가 같은 이름을 쓰게 하는 것입니다. 전체 Run Passport
저장소, rerun lineage, advanced evidence model은 아직 구현하지 않습니다.

## 응답 필드

<!-- markdownlint-disable MD013 -->
| 필드 | 의미 | v0 출처 |
| --- | --- | --- |
| `contractVersion` | 계약 버전 | 상수 `run-passport-summary/v0` |
| `runPassportId` | Run Passport 식별자 | `ticket:{ticketId}` |
| `runPassportUrl` | summary를 다시 조회할 상대 URL | `/api/tickets/{id}/run-passport` |
| `ticketId` | 원본 티켓 ID | `Ticket.Id` |
| `title` | 원본 티켓 제목 | `Ticket.Title` |
| `status` | 티켓 상태 문자열 | `Pending`, `Running`, `WaitingApproval`, `Completed`, `Failed`, `Cancelled` |
| `summary` | 사람이 읽을 짧은 상태 요약 | 티켓 상태와 실패/PR 정보에서 파생 |
| `createdAt`, `startedAt`, `completedAt` | 티켓 lifecycle timestamp | `Ticket` timestamp |
| `updatedAt` | 가장 최근 lifecycle timestamp | `CompletedAt ?? StartedAt ?? CreatedAt` |
| `issueTracker`, `externalIssueKey`, `externalIssueUrl` | 연결된 외부 이슈 참조 | `Jira`, `Linear`, 또는 `null` |
| `pullRequestUrl` | agent 결과 PR/MR URL | `Ticket.PrUrl` |
| `notionDocumentId`, `notionDocumentUrl` | Notion lifecycle 문서 참조 | v0에서는 `null`; ZZA-52 소유 |
| `testSummary`, `residualRiskSummary` | 검증/리스크 요약 | v0에서는 `null`; 후속 evidence/PR packet 소유 |
| `failureReason` | 실패 또는 취소 사유 | secret assignment와 local path를 가린 `Ticket.FailReason` |
<!-- markdownlint-enable MD013 -->

## 예시

아래 예시는 전체 응답 필드를 보여 줍니다.

```json
{
  "contractVersion": "run-passport-summary/v0",
  "runPassportId": "ticket:00000000-0000-0000-0000-000000000000",
  "runPassportUrl": "/api/tickets/00000000-0000-0000-0000-000000000000/run-passport",
  "ticketId": "00000000-0000-0000-0000-000000000000",
  "title": "Build feature",
  "status": "Completed",
  "summary": "Ticket completed with a pull request.",
  "createdAt": "2026-07-09T00:00:00+00:00",
  "startedAt": "2026-07-09T00:01:00+00:00",
  "completedAt": "2026-07-09T00:05:00+00:00",
  "updatedAt": "2026-07-09T00:05:00+00:00",
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

## 코드 위치

- Contract: `src/DevAutomation.Core/Contracts/RunPassportContracts.cs`
- API 라우팅: `src/DevAutomation.Api/Program.cs`
- 원본 도메인: `src/DevAutomation.Core/Entities/Ticket.cs`
- 테스트: `tests/DevAutomation.Tests/RunPassportContractTests.cs`

## 현재 한계

- 별도 Run Passport persistence table은 아직 없습니다.
- rerun lineage와 replay 기록은 아직 없습니다.
- Notion lifecycle 문서 URL은 ZZA-52가 저장 방식을 정하기 전까지 `null`입니다.
- PR review packet의 test/residual risk 요약은 ZZA-55 또는 별도 evidence collector가
  소유하기 전까지 `null`입니다.
- 이 endpoint는 execution log raw content, approval payload, secret, local path를
  포함하지 않습니다.
