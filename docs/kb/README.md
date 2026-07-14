# ReplaceMe KB

> Notion canonical: [KB](https://app.notion.com/p/397ef22ad4fc818e91caf5a9991eaed1)
>
> 기준일: 2026-07-14

## 검증된 핵심 학습

1. API와 worker는 process boundary를 분리해야 long-running agent lifecycle이 API
   availability를 끌어내리지 않습니다.
2. Kafka는 at-least-once 성격을 전제로 terminal ticket replay를 idempotent하게
   처리해야 합니다.
3. DB 저장과 Kafka publish는 현재 원자적이지 않습니다. broker 장애 뒤 orphan
   `Pending` ticket을 복구하려면 outbox/reconciler가 필요합니다.
4. host Docker socket은 host 권한에 준합니다. readiness warning/guard는
   production-grade 격리를 대신하지 않습니다.
5. 현재 Redpanda는 비영속입니다. `docker compose down` 뒤 queue, DLQ, consumer
   offset이 사라질 수 있으므로 일시 정지는 `stop/start`를 사용합니다.
6. OTel은 service/runtime telemetry, Langfuse는 향후 AI-run telemetry로 책임을
   분리합니다.
7. secret redaction은 log·trace·DLQ·Notion·PR sink보다 앞선 공통 경계여야 합니다.
   literal scan만으로 파생·인코딩된 유출 부재를 증명할 수는 없습니다.
8. ZZA-52/55 같은 design ticket의 Done은 자동화 구현 완료가 아닙니다.
9. Notion을 canonical로 두고 로컬 docs를 동기화해야 두 문서 surface가 drift하지
   않습니다.
10. PR에는 문제·변경·테스트·데모뿐 아니라 미실행 검증과 residual risk를 남깁니다.

## 운영 시 바로 적용할 규칙

- `/health`와 readiness를 구분합니다.
- `POST .../doctor`는 외부 write이므로 QA target에서만 실행합니다.
- Compose interpolation 값은 container recreate 뒤에 적용됩니다.
- DLQ replay 전에 Ticket 상태·side effect·offset을 확인합니다.
- file log와 Compose stdout도 configured secret value와 token pattern으로 검사합니다.
- 외부 provider/full agent QA는 scratch repo와 최소 scope credential로 실행합니다.

## 관련 Notion 문서

- [실패/재시도 기록 규칙](https://app.notion.com/p/397ef22ad4fc81189441d7ff86095dae)
- [운영 팁과 주의사항](https://app.notion.com/p/397ef22ad4fc81559247c9d1a561119b)
- [PR 설명 규칙](https://app.notion.com/p/397ef22ad4fc81fab8d8dd69b63a166b)
- [서브모듈과 zWorkspaces 운영 팁](https://app.notion.com/p/397ef22ad4fc8120ba46db0fc99922f2)
- [문서 이중 발행과 Notion 기준 동기화](https://app.notion.com/p/39cef22ad4fc812bab17f500c32b9974)
