# ReplaceMe 기능 문서

현재 구현된 DevAutomation 기능을 기능 단위로 나눈 문서 모음입니다.

## 문서 종류

<!-- markdownlint-disable MD013 -->
| 종류 | 먼저 볼 문서 | 목적 |
| --- | --- | --- |
| 기능 현황 | [`features/feature-status.md`](./features/feature-status.md) | 현재 구현된 기능과 전체 흐름을 한눈에 봅니다. |
| 기능 설명 | [`features/`](./features/) | 기능별로 “무엇을 하는지”와 “로직이 어떻게 흐르는지”를 봅니다. |
| QA 실 테스트 | [`qa/`](./qa/) | 로컬 실행부터 기능 단위 수동 테스트 체크리스트를 봅니다. |
| 구현 계획 | [`plans/`](./plans/) | 아직 만들 기능을 어떤 순서로 개발할지 봅니다. |
| 아이디에이션 | [`ideation/`](./ideation/) | 왜 이 방향을 선택했는지 배경을 봅니다. |
| HTML 요약 | [`feature-overview.html`](./feature-overview.html) | 전체 기능을 시각적으로 빠르게 훑습니다. |
<!-- markdownlint-enable MD013 -->

## 빠른 보기

<!-- markdownlint-disable MD013 -->
- [`features/feature-status.md`](./features/feature-status.md) — 현재 기능 지도와
  초보자용 용어 풀이
- [`feature-overview.html`](./feature-overview.html) — 전체 기능을 한 화면에서
  보는 HTML 요약
- [`pr-2-5-feature-summary.html`](./pr-2-5-feature-summary.html) — 분할 PR #2-#5
  기능과 배포 순서 요약
- [`features/ticket-management.md`](./features/ticket-management.md) — 티켓 API와
  상태 전이
- [`features/agent-execution.md`](./features/agent-execution.md) — Kafka/Docker
  기반 코딩 에이전트 실행
- [`features/approval-flow.md`](./features/approval-flow.md) — MCP
  `approval_prompt` 승인 플로우
- [`features/slack-integration.md`](./features/slack-integration.md) — Slack 알림,
  버튼, 서명 검증
- [`features/persistence-observability.md`](./features/persistence-observability.md) —
  PostgreSQL 모델, 로그, redaction
- [`features/local-operations.md`](./features/local-operations.md) — 로컬 실행,
  설정, 헬스체크
- [`features/readiness-profile.md`](./features/readiness-profile.md) — ZZA-51
  `personal-github-linear-notion` readiness profile
- [`features/run-passport.md`](./features/run-passport.md) — Run Passport v0
  summary contract
- [`qa/README.md`](./qa/README.md) — 로컬 실행부터 기능별 실 테스트를 진행하는
  QA 문서 목차
- [`plans/2026-07-08-001-feat-personal-github-linear-notion-profile-plan.md`](./plans/2026-07-08-001-feat-personal-github-linear-notion-profile-plan.md) —
  ZZA-51 `personal-github-linear-notion` readiness profile 구현 계획
- [`plans/2026-07-13-001-feat-infra-foundation-roadmap-plan.md`](./plans/2026-07-13-001-feat-infra-foundation-roadmap-plan.md) —
  ZZA-59~64 인프라 로드맵과 선후관계
- [`plans/2026-07-13-002-feat-notion-lifecycle-pattern-bank-plan.md`](./plans/2026-07-13-002-feat-notion-lifecycle-pattern-bank-plan.md) —
  ZZA-52 Notion lifecycle/pattern bank 설계
- [`plans/2026-07-13-003-feat-github-pr-review-packet-plan.md`](./plans/2026-07-13-003-feat-github-pr-review-packet-plan.md) —
  ZZA-55 GitHub PR review packet 설계
- [`ideation/2026-07-08-replaceme-github-linear-notion-dev-automation-ideation.html`](./ideation/2026-07-08-replaceme-github-linear-notion-dev-automation-ideation.html) —
  GitHub·Linear·Notion 개인 개발 자동화 아이디에이션
<!-- markdownlint-enable MD013 -->

## 현재 구현 범위

ReplaceMe는 지금 “요구사항 티켓 → Kafka 큐 → 격리 컨테이너에서 코딩 에이전트
실행 → 필요 시 notifier 승인 → 로그/상태 저장 → Run Passport v0 요약 조회”의
핵심 수직 흐름을 갖춘 상태입니다.

아직 웹 UI, API 인증, run replay, 운영 배포 설정은 v1 범위 밖입니다.

## 용어 빠른 풀이

| 용어 | 쉬운 설명 |
| --- | --- |
| Kafka API broker | 티켓 실행 작업을 worker에게 전달하는 메시지 큐입니다. |
| MCP | 코딩 에이전트가 외부 도구를 안전하게 호출하게 해주는 연결 규격입니다. |
| Provider | GitHub/GitLab, Linear/Jira처럼 교체 가능한 외부 도구 구현입니다. |
| Notifier | Slack/Gmail처럼 사용자에게 알림이나 승인 요청을 보내는 구현입니다. |
| PR/MR | GitHub Pull Request 또는 GitLab Merge Request입니다. |
| Redaction | 로그에 secret 값이 남지 않게 `[REDACTED]`로 가리는 처리입니다. |
| Readiness profile | 실행 전에 도구와 권한이 준비됐는지 확인하는 사전 점검 프로필입니다. |
