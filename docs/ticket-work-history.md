# 티켓 작업 이력

> Notion canonical:
> [개발 문서 → 티켓](https://app.notion.com/p/397ef22ad4fc8096a943cf053c12728a)
>
> Linear project:
> [ReplaceMe](https://linear.app/zzanghyunmoo/project/replaceme-f4f260cabfe8)
>
> 기준일: 2026-07-14

## 상태 원장

<!-- markdownlint-disable MD013 -->
| 티켓/상태 | 브레인스토밍·요구사항 | 계획 | 코드 | 문서 | 검증·완료 의미 |
| --- | --- | --- | --- | --- | --- |
| [ZZA-50](https://linear.app/zzanghyunmoo/issue/ZZA-50) Done | [Notion ticket](https://app.notion.com/p/39def22ad4fc8132bc30f70a3a69eed2) | N/A — 관리 surface 세팅 | N/A — product code 없음 | [프로젝트 위키](https://app.notion.com/p/397ef22ad4fc81f19555c64e0afa36b4) | 위키·Linear project·초기 이슈 생성 |
| [ZZA-51](https://linear.app/zzanghyunmoo/issue/ZZA-51) Done | [Notion 단계 문서](https://app.notion.com/p/397ef22ad4fc81f723a4225d044e) | [local plan](./plans/2026-07-08-001-feat-personal-github-linear-notion-profile-plan.md) | [PR #6](https://github.com/zzanghyunmoo/ReplaceMe/pull/6), gate `504dc41` | [PR #7](https://github.com/zzanghyunmoo/ReplaceMe/pull/7), [feature](./features/readiness-profile.md) | [QA 01](./qa/01-readiness-profile.md), 현재 suite 76개 |
| [ZZA-52](https://linear.app/zzanghyunmoo/issue/ZZA-52) Done | [Notion ticket](https://app.notion.com/p/39cef22ad4fc81bd663adf4bdcc) | [local plan](./plans/2026-07-13-002-feat-notion-lifecycle-pattern-bank-plan.md) | N/A — design-only scope | [PR #15](https://github.com/zzanghyunmoo/ReplaceMe/pull/15) | 설계 완료, 자동 hook/persistence 미구현 |
| [ZZA-53](https://linear.app/zzanghyunmoo/issue/ZZA-53) Backlog | [설계 노트](https://app.notion.com/p/397ef22ad4fc816cb879fa8af6599f96) | [infra roadmap](./plans/2026-07-13-001-feat-infra-foundation-roadmap-plan.md) | N/A — 미착수 | 설계 노트만 | Backlog, Linear execution grammar 미구현 |
| [ZZA-54](https://linear.app/zzanghyunmoo/issue/ZZA-54) Backlog | [설계 노트](https://app.notion.com/p/397ef22ad4fc8106b057f4b21d6e26f7) | [infra roadmap](./plans/2026-07-13-001-feat-infra-foundation-roadmap-plan.md) | N/A — 미착수 | 설계 노트만 | ZZA-51/64와 겹치지 않는 범위 재정의 필요 |
| [ZZA-55](https://linear.app/zzanghyunmoo/issue/ZZA-55) Done | [Notion ticket](https://app.notion.com/p/39cef22ad4fc81119d09f8ff8736d3fe) | [local plan](./plans/2026-07-13-003-feat-github-pr-review-packet-plan.md) | N/A — design-only scope | [PR #16](https://github.com/zzanghyunmoo/ReplaceMe/pull/16) | 설계 완료, 자동 builder/publication 미구현 |
| [ZZA-56](https://linear.app/zzanghyunmoo/issue/ZZA-56) Done | [Notion 단계 문서](https://app.notion.com/p/398ef22ad4fc81e694daee939381c3dc) | [local plan](./plans/2026-07-09-001-feat-run-passport-minimal-contract-plan.md) | [PR #8](https://github.com/zzanghyunmoo/ReplaceMe/pull/8), `7cb1db4` | [PR #9](https://github.com/zzanghyunmoo/ReplaceMe/pull/9), [feature](./features/run-passport.md) | `RunPassportContractTests`, v0 baseline 완료; ZZA-66에서 v1 hardening |
| [ZZA-57](https://linear.app/zzanghyunmoo/issue/ZZA-57) Done | [Notion 단계 문서](https://app.notion.com/p/398ef22ad4fc813b8ac4c596660b039c) | N/A — ZZA-56 QA에서 분리, Notion child에 기록 | [PR #11](https://github.com/zzanghyunmoo/ReplaceMe/pull/11), `c1f30ab` | [PR #10](https://github.com/zzanghyunmoo/ReplaceMe/pull/10), [local ops](./features/local-operations.md) | [QA 00](./qa/00-local-runbook.md), Redpanda 계약 완료 |
| [ZZA-58](https://linear.app/zzanghyunmoo/issue/ZZA-58) Done | [Notion ticket](https://app.notion.com/p/39cef22ad4fc81e09996d9178f37cc85) | [infra precondition](./plans/2026-07-13-001-feat-infra-foundation-roadmap-plan.md) | [PR #13](https://github.com/zzanghyunmoo/ReplaceMe/pull/13), `8a04145` | [README](../README.md), [QA 00](./qa/00-local-runbook.md) | .NET 9 restore/build/test와 Docker build path |
| [ZZA-59](https://linear.app/zzanghyunmoo/issue/ZZA-59) Done | [Notion ticket](https://app.notion.com/p/39cef22ad4fc81f68514ca2aab88fa66) | [infra roadmap U1](./plans/2026-07-13-001-feat-infra-foundation-roadmap-plan.md) | [PR #14](https://github.com/zzanghyunmoo/ReplaceMe/pull/14), `37d2e7a`, `28aceb6` | [agent](./features/agent-execution.md), [local ops](./features/local-operations.md) | `HostingCompositionTests`, API/Worker 분리 완료 |
| [ZZA-60](https://linear.app/zzanghyunmoo/issue/ZZA-60) Backlog | Linear 요구사항 | [infra roadmap U4](./plans/2026-07-13-001-feat-infra-foundation-roadmap-plan.md) | N/A — options만 존재 | roadmap만 | Langfuse sink/trace 미구현 |
| [ZZA-61](https://linear.app/zzanghyunmoo/issue/ZZA-61) Done | [Notion ticket](https://app.notion.com/p/39cef22ad4fc81b49f6dc0e4695239d4) | [infra roadmap U2](./plans/2026-07-13-001-feat-infra-foundation-roadmap-plan.md) | [PR #17](https://github.com/zzanghyunmoo/ReplaceMe/pull/17), `a6b68cf` | [agent](./features/agent-execution.md), [QA 03](./qa/03-agent-execution.md) | retry/DLQ/message/replay tests |
| [ZZA-62](https://linear.app/zzanghyunmoo/issue/ZZA-62) Done | [Notion ticket](https://app.notion.com/p/39cef22ad4fc81b588901e065574c84ed) | [infra roadmap U3](./plans/2026-07-13-001-feat-infra-foundation-roadmap-plan.md) | [PR #18](https://github.com/zzanghyunmoo/ReplaceMe/pull/18), `340e919` | [persistence](./features/persistence-observability.md), [QA 06](./qa/06-persistence-observability.md) | Compose profile config와 local telemetry smoke 계약 |
| [ZZA-63](https://linear.app/zzanghyunmoo/issue/ZZA-63) Backlog | Linear 요구사항 | [infra roadmap U5](./plans/2026-07-13-001-feat-infra-foundation-roadmap-plan.md) | N/A — options만 존재 | roadmap만 | LiteLLM compatibility spike 미실행 |
| [ZZA-64](https://linear.app/zzanghyunmoo/issue/ZZA-64) Done | [Notion ticket](https://app.notion.com/p/39cef22ad4fc81a3ba01449e7515510b) | [infra roadmap U6](./plans/2026-07-13-001-feat-infra-foundation-roadmap-plan.md) | [PR #19](https://github.com/zzanghyunmoo/ReplaceMe/pull/19), `ea38509` | [agent](./features/agent-execution.md), [readiness](./features/readiness-profile.md) | `AgentHardeningTests`, local boundary hardening 완료 |
| [ZZA-65](https://linear.app/zzanghyunmoo/issue/ZZA-65) In Review | [Notion ticket](https://app.notion.com/p/39def22ad4fc81df9f54dc4f9b5a789f) | N/A — merged infra batch 후속 정리 | [PR #20](https://github.com/zzanghyunmoo/ReplaceMe/pull/20) | infra/QA 문서 최소 동기화 | 전체 test 52개, build/diff check |
| [ZZA-66](https://linear.app/zzanghyunmoo/issue/ZZA-66) In Review | [Notion ticket](https://app.notion.com/p/39def22ad4fc810f8201f987d326c761) | [Run Passport plan](./plans/2026-07-09-001-feat-run-passport-minimal-contract-plan.md) | [PR #21](https://github.com/zzanghyunmoo/ReplaceMe/pull/21) | [Run Passport v1](./features/run-passport.md) | focused 27개, branch test 74개 |
| [ZZA-67](https://linear.app/zzanghyunmoo/issue/ZZA-67) In Review | [Notion ticket](https://app.notion.com/p/39def22ad4fc81a0ba03f1392f8e975e) | N/A — operations review hardening | [PR #22](https://github.com/zzanghyunmoo/ReplaceMe/pull/22) | [local ops](./features/local-operations.md), [QA 00](./qa/00-local-runbook.md) | composition 8개, branch test 76개 |
| [ZZA-68](https://linear.app/zzanghyunmoo/issue/ZZA-68) In Review | [Notion ticket](https://app.notion.com/p/39def22ad4fc8146bad4ef37d06519cf) | N/A — canonical docs sync scope | [PR #23](https://github.com/zzanghyunmoo/ReplaceMe/pull/23) | [architecture](./architecture.md), [guides](./guides/operator-guide.md), [KB](./kb/README.md) | markdownlint/link check, branch test 76개 |
<!-- markdownlint-enable MD013 -->

## 단계별 산출물 위치

- 아이디에이션: `docs/ideation/`
- 구현 계획: `docs/plans/`
- 기능별 현재 로직: `docs/features/`
- 운영 검증: `docs/qa/`
- canonical 티켓 폴더: Notion `개발 문서 → 티켓 → ZZA-*`

ZZA-51과 ZZA-56/66은 독립 plan과 단계별 Notion child page가 있습니다. ZZA-59,
61, 62, 64는 공통 인프라 roadmap에서 U-ID로 계획했습니다. ZZA-57/58과 ZZA-65,
67, 68은 장애·후속 정리·검증에서 분리된 티켓이라 독립 로컬 brainstorm 문서 대신
Linear 설명과 Notion 티켓 문서가 요구사항 근거입니다.

## 완료 판단 규칙

- Linear Done만으로 product 구현 완료를 주장하지 않습니다.
- 설계 티켓은 설계 계약의 Done과 runtime automation의 Done을 구분합니다.
- 코드 티켓은 main 도달 commit, test, feature/QA 문서를 함께 확인합니다.
- 티켓 child의 기능 현황은 변경 요약입니다. 제품 전체 canonical 상태는 Notion
  `디자인 문서 → 기능 현황`입니다.
- rebased feature branch SHA보다 canonical main commit과 PR 번호를 우선합니다.
