# ReplaceMe 기능 문서

현재 구현된 DevAutomation 기능을 기능 단위로 나눈 문서 모음입니다.

## 빠른 보기

<!-- markdownlint-disable MD013 -->
- [`feature-overview.html`](./feature-overview.html) — 전체 기능을 한 화면에서
  보는 HTML 요약
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
<!-- markdownlint-enable MD013 -->

## 현재 구현 범위

ReplaceMe는 지금 “요구사항 티켓 → Kafka 큐 → 격리 컨테이너에서 코딩 에이전트
실행 → 필요 시 notifier 승인 → 로그/상태 저장”의 핵심 수직 흐름을 갖춘
상태입니다.

아직 웹 UI, API 인증, 재시도 정책, 운영 배포 설정은 v1 범위 밖입니다.
