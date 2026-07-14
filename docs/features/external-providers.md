# 외부 Provider 연동

> Notion canonical:
> [외부 Provider 연동](https://app.notion.com/p/39def22ad4fc8114b9d2e969b5edf688)

## 지원 구현

- Issue tracker: Jira, Linear, None
- Document tool: Notion, Confluence, None
- Remote repository: GitHub, GitLab
- Notifier: Slack, Gmail, None
- Coding agent: Claude Code

Startup 설정에 따라 각 영역에서 하나의 active provider를 DI로 선택합니다.

## 로직

1. Ticket 생성 시 외부 issue를 새로 만들거나 기존 reference를 연결할 수 있습니다.
2. document endpoint는 active Notion/Confluence 구현을 호출합니다.
3. Agent container는 선택된 GitHub/GitLab provider에 맞춰 branch를 push하고 PR/MR을
   생성합니다.
4. API/worker의 Ticket 상태 알림은 선택된 notifier를 사용합니다.
5. Agent container의 Approval MCP는 provider/Gmail 설정을 전달받지 않아 현재 Slack
   기본값을 사용합니다.
6. 현재 readiness는 generic provider doctor가 아니라 고정된
   `personal-github-linear-notion` profile의 GitHub/Linear/Notion 접근과 agent image
   capability를 확인합니다.

## 현재 한계

- 실제 credential이 필요한 provider E2E는 기본 test suite에서 검증하지 않습니다.
- Linear issue 하나만으로 실행하는 grammar는 ZZA-53 backlog입니다.
- ZZA-52 Notion lifecycle 자동 hook과 ZZA-55 PR packet 자동 publication은 아직
  구현되지 않았습니다.
- Gmail/None 선택은 agent container Approval MCP에 전파되지 않습니다. Slack
  token/channel이 남아 있으면 `Notifier=None`이어도 MCP가 Slack 알림을 시도할 수
  있으므로 무알림 QA에서는 Slack 값도 비웁니다.
- GitLab/Jira/Confluence용 generic readiness profile은 없습니다.

## 코드 위치

- `src/DevAutomation.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`
- `src/DevAutomation.Infrastructure/IssueTrackers/`
- `src/DevAutomation.Infrastructure/DocumentTools/`
- `src/DevAutomation.Infrastructure/RemoteRepositories/`
- `src/DevAutomation.Infrastructure/Notifications/`
