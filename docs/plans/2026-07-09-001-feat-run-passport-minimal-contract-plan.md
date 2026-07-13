---
title: Run Passport Minimal Contract - Plan
type: feat
date: 2026-07-09
topic: run-passport-minimal-contract
artifact_contract: ce-unified-plan/v1
artifact_readiness: implementation-ready
product_contract_source: ce-plan-bootstrap
execution: code
---

<!-- markdownlint-disable -->

# Run Passport Minimal Contract - Plan

## Goal Capsule

| Field | Value |
| --- | --- |
| Objective | Implement the first, minimal Run Passport summary contract for ReplaceMe so follow-up Notion lifecycle and PR review packet work can share one field/link shape. |
| Product authority | User request to proceed with ZZA-56 after the ZZA-51 readiness step, plus `docs/solutions/workflow-issues/run-passport-contract-before-dependent-follow-ups.md` in the workspace knowledge store. |
| Execution profile | Code implementation in the ReplaceMe repo. |
| Stop conditions | Stop before building full Run Passport persistence, rerun lineage, Notion lifecycle page updates, PR packet generation, or Linear issue execution grammar. |
| Tail ownership | LFG owns implementation, review, commit, PR creation, and CI watching after this plan. |

---

## Product Contract

Product Contract preservation: no prior requirements-only artifact exists for ZZA-56 in this repo. This plan bootstraps the smallest implementation-ready contract from the user's approved sequencing decision.

### Summary

ReplaceMe needs a stable Run Passport summary shape before ZZA-52 and ZZA-55 can build Notion lifecycle pages and PR review packets in parallel. The first slice must expose a contract that identifies an automation run, points to known Linear/GitHub/Notion surfaces where available, summarizes status/evidence/risk fields, and clearly marks unavailable fields as null rather than letting downstream features invent their own names.

### Problem Frame

The ZZA-51 plan deliberately kept Run Passport, PR packet generation, Notion lifecycle documents, and Linear issue execution grammar out of readiness scope. The follow-up split is correct, but ZZA-52 and ZZA-55 both need the same run identity and summary fields. If each ticket invents its own names for run links, status, evidence, and residual risk, ZZA-56 will later have to normalize incompatible contracts.

### Actors

- A1. **ReplaceMe API caller.** Fetches a ticket and its Run Passport summary.
- A2. **Notion lifecycle document worker.** Later consumes the summary to render/update ticket-scoped Notion pages.
- A3. **PR review packet worker.** Later consumes the summary to populate the PR body and residual-risk section.
- A4. **Linear issue execution worker.** Later attaches or displays the Passport reference when a Linear issue triggers an automation run.
- A5. **Reviewer/operator.** Reads the summary to understand what happened without scraping logs.

### Requirements

**Contract identity**

- R1. The contract must expose a stable contract version string so future consumers can detect changes.
- R2. The contract must expose a stable Run Passport identifier derived from the ticket identifier for this first non-persistent slice.
- R3. The contract must expose a relative API URL for fetching the Run Passport summary.

**Run surface links**

- R4. The contract must include the source ticket id and title.
- R5. The contract must include the external issue provider/key/url when the ticket has an issue tracker reference.
- R6. The contract must include the PR URL when the agent run completed with one.
- R7. The contract must reserve Notion document id/url fields as nullable until ZZA-52 owns lifecycle document persistence.

**Status and review support**

- R8. The contract must include ticket status and a short human summary.
- R9. The contract must include failure reason when present.
- R10. The contract must reserve test summary and residual risk summary as nullable fields until a later evidence collector or PR packet worker owns them.
- R11. The contract must include created, started, completed, and updated timestamps where the current ticket model can supply them.

**Boundary**

- R12. This slice must not add a new persisted Run Passport table.
- R13. This slice must not implement rerun lineage, Notion page creation/update, PR body generation, or Linear issue execution grammar.
- R14. The contract must not expose secrets, tokens, local filesystem paths, or raw execution log content.

### Acceptance Examples

- AE1. Given a pending ticket with an external Linear issue URL, when the API caller fetches its Run Passport summary, then the response includes `contractVersion`, a deterministic `runPassportId`, the ticket status, the external issue fields, and null PR/Notion/test/risk fields.
- AE2. Given a completed ticket with a PR URL, when the API caller fetches its Run Passport summary, then the response includes the PR URL and a completion summary.
- AE3. Given a failed ticket with a failure reason, when the API caller fetches its Run Passport summary, then the response includes the failure reason and a failure summary.
- AE4. Given an unknown ticket id, when the API caller fetches its Run Passport summary, then the endpoint returns 404.

### Scope Boundaries

**In scope**

- Domain/contract model for the minimal Run Passport summary.
- A ticket-derived factory or service that builds the summary without persisting a new table.
- A read-only API endpoint for `GET /api/tickets/{id}/run-passport`.
- Unit tests for contract mapping and endpoint-visible behavior where practical.
- Feature documentation that marks the contract as v0/minimal and non-persistent.

**Out of scope**

- Full Run Passport persistence shape.
- Rerun lineage and replay semantics.
- Notion lifecycle page creation or updates.
- GitHub PR review packet body generation.
- Linear issue execution grammar or runnable issue validation.
- Secret scanning beyond not including logs/secrets in this summary model.

---

## Implementation Units

### U1. Add the Run Passport summary contract

**Goal:** Add a stable, serializable Core contract that downstream features can consume without knowing Ticket internals.

**Files:**

- Create `src/DevAutomation.Core/Contracts/RunPassportContracts.cs`
- Modify `src/DevAutomation.Core/DevAutomation.Core.csproj` only if needed by namespace/build rules
- Create or modify `tests/DevAutomation.Tests/RunPassportContractTests.cs`

**Approach:**

- Add `RunPassportSummaryResponse` as a public sealed record under `DevAutomation.Core.Contracts`.
- Include these fields in the first contract: `ContractVersion`, `RunPassportId`, `RunPassportUrl`, `TicketId`, `Title`, `Status`, `Summary`, `CreatedAt`, `StartedAt`, `CompletedAt`, `UpdatedAt`, `IssueTracker`, `ExternalIssueKey`, `ExternalIssueUrl`, `PullRequestUrl`, `NotionDocumentId`, `NotionDocumentUrl`, `TestSummary`, `ResidualRiskSummary`, `FailureReason`.
- Add a static factory such as `From(Ticket ticket)` or a small service method. The first slice can derive `RunPassportId` as `ticket:{ticket.Id}` and `RunPassportUrl` as `/api/tickets/{ticket.Id}/run-passport`.
- Keep Notion/test/risk fields nullable and explicitly unfilled because their owners are later tickets.
- Generate `Summary` from the ticket status and known fields without reading execution logs.

**Patterns to follow:**

- `src/DevAutomation.Core/Contracts/TicketContracts.cs` for response record shape and `From(...)` factory style.
- `tests/DevAutomation.Tests/TicketIssueTrackerTests.cs` for domain-contract style assertions.

**Execution note:** Start with a failing test for `RunPassportSummaryResponse.From(ticket)` before adding the contract.

**Test scenarios:**

- Pending ticket maps to `contractVersion = run-passport-summary/v0`, deterministic `runPassportId`, relative URL, title, status, timestamps, and null downstream-only fields.
- Ticket with external issue reference maps provider/key/url to the summary.
- Completed ticket maps PR URL and completion summary.
- Failed ticket maps failure reason and failure summary.

**Verification:**

- `dotnet test DevAutomation.sln --no-restore --filter RunPassportContractTests`

### U2. Expose the read-only Run Passport endpoint

**Goal:** Add the first API surface that downstream tools can call to retrieve the contract.

**Files:**

- Modify `src/DevAutomation.Api/Program.cs`
- Modify or create `tests/DevAutomation.Tests/RunPassportEndpointTests.cs` if endpoint testing can be done without broad test infrastructure churn

**Approach:**

- Add `GET /api/tickets/{id:guid}/run-passport` near the existing ticket read endpoints.
- Load the ticket with `AsNoTracking()` and return 404 if missing.
- Return `RunPassportSummaryResponse.From(ticket)` for existing tickets.
- Do not include execution logs or approval request payloads in this first endpoint.
- If full minimal-API integration tests require adding heavy web test infrastructure, prefer unit coverage of the contract plus a small endpoint mapping test only if the existing project already supports it.

**Patterns to follow:**

- Existing `GET /api/tickets/{id:guid}` endpoint in `src/DevAutomation.Api/Program.cs`.
- Existing response factory style in `src/DevAutomation.Core/Contracts/TicketContracts.cs`.

**Execution note:** Prefer proof-first coverage if a lightweight seam exists. If not, record a deliberate no-test exception for the minimal endpoint wrapper and cover the behavior in U1 mapping tests.

**Test scenarios:**

- Existing ticket returns HTTP 200 with the summary shape.
- Missing ticket returns HTTP 404.
- Endpoint does not enqueue work, publish reports, or mutate ticket state.

**Verification:**

- Focused test command for endpoint tests if added.
- Otherwise `dotnet test DevAutomation.sln --no-restore --filter RunPassportContractTests` plus `dotnet build DevAutomation.sln --no-restore`.

### U3. Document the v0 contract and boundaries

**Goal:** Make the contract discoverable to ZZA-52 and ZZA-55 implementers without promising full Passport persistence.

**Files:**

- Create `docs/features/run-passport.md`
- Modify `docs/features/feature-status.md`
- Modify `docs/README.md`
- Modify `README.md` if the API endpoint list needs the new route

**Approach:**

- Document the endpoint, contract version, fields, nullable placeholders, and explicit non-goals.
- Link the feature status page to the new document.
- Mention that ZZA-52 and ZZA-55 should consume this v0 summary rather than invent separate field names.
- Keep docs honest: no persistence table, rerun lineage, Notion lifecycle update, or PR packet generation in this slice.

**Patterns to follow:**

- `docs/features/ticket-management.md` for endpoint documentation style.
- `docs/features/readiness-profile.md` for explicit boundaries and follow-up notes.

**Execution note:** Docs-only unit; no test-first step required. Validate with markdown hygiene and build/test smoke.

**Test scenarios:**

- Documentation names the endpoint and contract version.
- Documentation marks downstream-owned nullable fields clearly.
- Documentation does not claim full ZZA-56 persistence or rerun lineage.

**Verification:**

- `git diff --check`
- `dotnet test DevAutomation.sln --no-restore --filter RunPassportContractTests`
- `dotnet build DevAutomation.sln --no-restore`

---

## Dependencies and Sequencing

1. U1 must land first because U2 and U3 depend on the contract names.
2. U2 follows U1 and should stay a thin read-only wrapper.
3. U3 follows U1/U2 so documentation matches the final field names.

Parallel execution is not recommended inside this small plan because U1 and U2 both touch public contract shape and U3 should reflect the final names.

## Verification Contract

Run these before returning implementation to LFG:

```bash
dotnet test DevAutomation.sln --no-restore --filter RunPassportContractTests
dotnet build DevAutomation.sln --no-restore
git diff --check
```

If `--no-restore` fails because the worktree has not restored packages, run `dotnet restore DevAutomation.sln` once, then rerun the commands above and record the deviation.

## Definition of Done

- `RunPassportSummaryResponse` exposes the v0 minimal contract.
- `GET /api/tickets/{id}/run-passport` returns the summary for existing tickets and 404 for missing tickets.
- Contract tests cover pending, external issue, completed, and failed ticket mapping.
- Documentation explains the v0 contract and downstream consumer boundary.
- No new Run Passport persistence table or rerun lineage behavior is introduced.
- Focused tests, build, and diff whitespace checks pass or any environmental blocker is recorded.

## Implementation-Time Unknowns

- Whether endpoint integration tests can be added without bringing in heavier web test packages. Resolve during U2; do not expand scope solely to build a test harness if contract tests already cover the behavior-bearing mapping.
- Whether the summary should include `UpdatedAt`. The current Ticket model does not have a general updated timestamp, so use the most recent non-null lifecycle timestamp or leave a clearly documented derived value.

## Risks

- **Contract creep:** adding persistence, lineage, or downstream rendering now would block the parallel follow-up wave. Keep the slice minimal.
- **False evidence:** test and residual-risk fields are placeholders in v0. Do not synthesize them from logs unless a later evidence collector owns that contract.
- **Naming drift:** downstream docs must use the exact v0 field names or explicitly version a new contract.
