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

## Current Implementation Status

The original ZZA-56 v0 baseline has already shipped:

| Unit | Shipped evidence | Current path |
| --- | --- | --- |
| U1 contract | [PR #8](https://github.com/zzanghyunmoo/ReplaceMe/pull/8), `7cb1db4` | `src/DevAutomation.Core/Contracts/RunPassportContracts.cs` |
| U2 endpoint | [PR #8](https://github.com/zzanghyunmoo/ReplaceMe/pull/8), `7cb1db4` | `src/DevAutomation.Api/Program.cs` |
| U3 docs | [PR #9](https://github.com/zzanghyunmoo/ReplaceMe/pull/9), `5889436` | `docs/features/run-passport.md` |

Do not recreate the types, route, tests, or feature document named below. This plan records the
gap-only hardening that promotes the shipped v0 baseline to the explicit v1 contract. ZZA-66
implements the corrections and verification described below.

## Product Contract

Product Contract preservation: no prior requirements-only artifact exists for ZZA-56 in this repo. This plan bootstraps the smallest implementation-ready contract from the user's approved sequencing decision.

### Summary

ReplaceMe needs one stable, machine-facing summary shape before ZZA-52 and ZZA-55 build
Notion lifecycle pages and PR review packets. The v1 passport is a mutable,
ticket-scoped workflow summary, not an immutable identity for each Kafka attempt or future
rerun. It standardizes current status, links, timestamps, and downstream-owned placeholders;
it does not by itself explain the full implementation outcome to a reviewer.

### Problem Frame

The ZZA-51 plan deliberately kept Run Passport, PR packet generation, Notion lifecycle
documents, and Linear issue execution grammar out of readiness scope. ZZA-52 and ZZA-55 need
the same ticket-scoped identity and field semantics. If each consumer invents names, absence
rules, or URL resolution behavior, a later contract revision must normalize incompatible
integrations.

### Actors

- A1. **ReplaceMe API caller.** Fetches a ticket-scoped Run Passport summary.
- A2. **Notion lifecycle document worker.** Consumes passport run/status/link fields and loads
  repository/base-branch context separately from the Ticket contract.
- A3. **PR review packet worker.** Consumes passport fields when evidence producers have filled
  them; v1 alone is not a complete review narrative.
- A4. **Linear issue execution worker.** Later attaches or displays the ticket-scoped Passport
  reference when a Linear issue triggers an automation run.
- A5. **Reviewer/operator.** Uses the summary as a compact index to current state and links,
  then reviews the PR and explicit evidence surfaces for what changed and what was verified.

### Requirements

**Contract identity**

- R1. The contract must expose a stable contract version string so future consumers can detect
  incompatible changes.
- R2. `runPassportId = ticket:{ticketId}` identifies one mutable ticket workflow. Consumers
  must not use it as an immutable Kafka-attempt, replay, or rerun identifier.
- R3. The shipped `runPassportUrl` field contains a relative API path, not an externally
  navigable URL. Consumers must resolve it against their configured ReplaceMe API base URL and
  must not embed the unresolved path as a Notion or PR backlink.

**Run surface links**

- R4. The contract must include the source ticket id and title. Repository URL and base branch
  remain explicit dependencies of the separate Ticket contract in v1.
- R5. External issue links must be absolute HTTPS URLs with no userinfo or credential-bearing
  query values and must match the selected provider's configured cloud or enterprise host.
- R6. Pull-request links follow the same validation rule and map empty, whitespace-only, or
  invalid values to `null`.
- R7. Notion document id/url fields remain nullable until ZZA-52 owns lifecycle persistence;
  their v1 absence meaning is `not-produced` and does not block the passport response.

**Status and review support**

- R8. The contract must include ticket status and a short machine-facing lifecycle summary.
  It must not claim to summarize code changes, tests, or residual risk when those producers have
  not supplied evidence.
- R9. A failed or cancelled ticket may expose only a public-safe generic failure summary. The
  contract must never return raw `Ticket.FailReason`.
- R10. Test and residual-risk summaries remain nullable until an evidence collector or PR
  packet worker owns them; their v1 absence meaning is `not-captured` and is non-blocking.
- R11. The contract must expose `CreatedAt`, `StartedAt`, `CompletedAt`, and the derived
  `LastLifecycleAt = CompletedAt ?? StartedAt ?? CreatedAt`. `LastLifecycleAt` is not a general
  mutation timestamp or synchronization cursor.

**Boundary**

- R12. This slice must not add a new persisted Run Passport table.
- R13. This slice must not implement immutable attempt identity, rerun lineage, Notion page
  creation/update, PR body generation, or Linear issue execution grammar.
- R14. The contract must not expose secrets, tokens, local filesystem paths, credential-bearing
  URLs, or raw execution log/failure content.
- R15. The endpoint inherits ReplaceMe's current trusted single-user local-only boundary. It has
  no authentication or authorization, binds through the loopback-only local Compose profile,
  and must not be exposed through a public proxy or tunnel.

### Nullable Field Availability

| Field | Producer | Becomes available | `null` meaning in v1 | Blocks response |
| --- | --- | --- | --- | --- |
| `pullRequestUrl` | Agent/repository provider | Valid PR/MR URL returned | Not created, unavailable, empty, or invalid | No |
| `notionDocumentId`, `notionDocumentUrl` | ZZA-52 lifecycle publisher | Lifecycle page persisted | Not produced | No |
| `testSummary` | Future evidence collector | Test evidence normalized | Not captured | No |
| `residualRiskSummary` | ZZA-55/evidence collector | Risk evidence normalized | Not captured | No |
| `failureReason` | Public-safe failure projector | Failed/cancelled outcome | No public failure detail | No |

These are documented v1 rules, not additional machine-readable absence-reason fields.

### Acceptance Examples

- AE1. A pristine pending ticket with a valid Linear URL returns the ticket-scoped id, relative
  API path, status, validated issue fields, and null downstream-owned fields.
- AE2. A completed ticket returns a validated PR URL when present; null, empty, whitespace-only,
  non-HTTPS, userinfo-bearing, credential-bearing, or unapproved-host values return `null`.
- AE3. A failed ticket whose raw reason contains assignments, headers, JSON credentials, URLs,
  Unix paths, or Windows paths returns only the generic public-safe failure summary.
- AE4. An unknown ticket id returns 404.
- AE5. A retrying ticket may be `Pending` with a non-null `StartedAt`; its summary says the
  ticket is pending retry rather than claiming execution never started.
- AE6. Running, WaitingApproval, and Cancelled tickets have explicit status and summary mappings.
- AE7. The endpoint returns the canonical camelCase JSON shape with explicit nullable fields and
  does not mutate state or enqueue work.

### Scope Boundaries

**In scope**

- Promoting the shipped ticket-derived v0 baseline to the explicit v1 contract without adding persistence.
- Exact C#/JSON wire shape, nullable-field rules, link normalization, and public-safe failure
  projection.
- Automated mapping, serialization, and HTTP 200/404/non-mutation tests.
- Feature documentation that marks the contract as ticket-scoped, local-only, minimal, and
  non-persistent.

**Out of scope**

- Full Run Passport persistence shape or immutable attempt/run identifiers.
- Rerun lineage and replay semantics.
- Notion lifecycle page creation or updates.
- GitHub PR review packet body generation.
- Linear issue execution grammar or runnable issue validation.
- Authentication/authorization implementation; public exposure remains prohibited.

---

## Implementation Units

### U1. Harden the shipped Run Passport summary contract

**Status:** Baseline shipped in PR #8; ZZA-66 implements the hardening gaps below.

**Goal:** Keep one stable ticket-scoped run/status/link shape while making its wire semantics,
absence rules, and public-data boundary explicit. Repository/base-branch context remains in the
separate Ticket contract.

**Files:**

- Modify `src/DevAutomation.Core/Contracts/RunPassportContracts.cs`
- Modify `tests/DevAutomation.Tests/RunPassportContractTests.cs`
- Move public-safe failure projection out of the static raw-string mapping seam if required by
  the final implementation shape

**Normative wire shape:**

| JSON field | C# type | Nullable | Semantics |
| --- | --- | --- | --- |
| `contractVersion` | `string` | No | Exact value `run-passport-summary/v1` |
| `runPassportId` | `string` | No | Mutable ticket key `ticket:{ticketId}` |
| `runPassportUrl` | `string` | No | Relative API path; consumer resolves base URL |
| `ticketId` | `Guid` | No | JSON UUID string |
| `title` | `string` | No | Ticket title |
| `status` | `string` | No | `Pending`, `Running`, `WaitingApproval`, `Completed`, `Failed`, `Cancelled` |
| `summary` | `string` | No | Machine-facing lifecycle summary only |
| `createdAt` | `DateTimeOffset` | No | ISO 8601/RFC 3339 JSON timestamp |
| `startedAt`, `completedAt` | `DateTimeOffset?` | Yes | ISO timestamp or explicit `null` |
| `lastLifecycleAt` | `DateTimeOffset` | No | `CompletedAt ?? StartedAt ?? CreatedAt`; not a general update clock |
| `issueTracker` | `string?` | Yes | `Jira`, `Linear`, or `null` |
| `externalIssueKey` | `string?` | Yes | Provider key or `null` |
| `externalIssueUrl` | `string?` | Yes | Validated absolute HTTPS URL or `null` |
| `pullRequestUrl` | `string?` | Yes | Validated absolute HTTPS URL or `null` |
| `notionDocumentId`, `notionDocumentUrl` | `string?` | Yes | `null` until ZZA-52 publishes |
| `testSummary`, `residualRiskSummary` | `string?` | Yes | `null` until evidence producer exists |
| `failureReason` | `string?` | Yes | Generic public-safe detail or `null`; never raw failure text |

ASP.NET JSON serialization uses camelCase property names and includes explicit nulls for the
nullable fields above. Add a canonical serialized fixture that locks this shape.

**Approach:**

- Preserve `ticket:{ticket.Id}` as the v1 ticket-workflow key and document that retries share it.
- Preserve `runPassportUrl` across the version bump, but treat it normatively as a relative path;
  each consumer combines it with a configured ReplaceMe API base URL.
- Validate issue and PR URLs as absolute HTTPS values without userinfo or credential-bearing
  query values and against configured provider hosts; invalid values become `null`.
- Map raw failure text to a generic allowlisted public summary such as `Execution failed.` or
  `Execution cancelled.` before constructing both `FailureReason` and `Summary`.
- Keep downstream-owned fields null according to the availability matrix above.
- Map a `Pending` ticket with non-null `StartedAt` to a pending-retry summary.
- Generate `Summary` from ticket state and public-safe values without reading execution logs.

**Patterns to follow:**

- `src/DevAutomation.Core/Contracts/TicketContracts.cs` for response record shape.
- `src/DevAutomation.Infrastructure/Agents/SecretRedactor.cs` for configured-value redaction
  coverage; the public contract must not create an Infrastructure dependency from Core.

**Execution note:** Add failing tests for every gap before changing the shipped contract.

**Test scenarios:**

- Pristine Pending and pending-retry tickets have distinct summaries.
- Running, WaitingApproval, Completed, Failed, and Cancelled mappings are explicit.
- Issue/PR links cover valid, null, empty, whitespace, non-HTTPS, userinfo, credential query,
  unexpected host, and enterprise-host cases.
- Failure input covers assignment, header, JSON, credential URL, Unix path, and Windows path
  forms and never appears raw in `FailureReason` or `Summary`.
- The canonical JSON fixture locks field names, enum strings, timestamp encoding, and explicit
  null placeholders.

**Verification:**

- `dotnet test DevAutomation.sln --no-restore --filter RunPassportContractTests`

### U2. Verify and constrain the shipped read-only endpoint

**Status:** Route shipped in PR #8; ZZA-66 adds wire-level and boundary coverage.

**Goal:** Prove the existing endpoint returns the normative contract without mutation and stays
inside ReplaceMe's trusted local-only deployment boundary.

**Files:**

- Modify `src/DevAutomation.Api/Program.cs` only for the minimal test-host seam if needed
- Modify `tests/DevAutomation.Tests/DevAutomation.Tests.csproj` to reference the API/test host
- Create `tests/DevAutomation.Tests/RunPassportEndpointTests.cs`

**Approach:**

- Keep `GET /api/tickets/{id:guid}/run-passport` read-only with `AsNoTracking()`.
- Require an automated HTTP test for 200, 404, exact camelCase JSON, enum strings, explicit null
  placeholders, and absence of queue/state mutations.
- Add the smallest `WebApplicationFactory<Program>` seam needed by the existing minimal API;
  do not replace endpoint verification with factory-only unit tests.
- State explicitly in API docs that the route has no auth/authz, is for loopback-bound trusted
  local use only, and must not be published through a proxy or tunnel.
- Do not return execution logs, raw approval payloads, or raw failure text.

**Test scenarios:**

- Existing ticket returns HTTP 200 with the exact normative JSON fixture.
- Missing ticket returns HTTP 404.
- Request leaves Ticket state, execution logs, and Kafka publish count unchanged.
- Unavailable nullable fields remain present as explicit JSON nulls.

**Verification:**

- `dotnet test DevAutomation.sln --no-restore --filter RunPassportEndpointTests`
- `dotnet test DevAutomation.sln --no-restore --filter RunPassportContractTests`
- `dotnet build DevAutomation.sln --no-restore`

### U3. Synchronize the hardened v1 contract and boundaries

**Status:** Baseline docs shipped in PR #9; ZZA-66 synchronizes the v1 corrections.

**Goal:** Make ticket-scoped identity, path resolution, separate Ticket context, nullable-field
semantics, local-only access, and unimplemented evidence producers discoverable without
promising full Passport persistence.

**Files:**

- Modify `docs/features/run-passport.md`
- Modify `docs/features/feature-status.md`
- Modify `docs/README.md`
- Modify `README.md` if the API endpoint list needs the new route

**Approach:**

- Document the endpoint, exact wire contract, nullable availability matrix, and non-goals.
- Explain that `runPassportId` is ticket-scoped and `runPassportUrl` is a relative path.
- Name `Ticket.RepoUrl` and `Ticket.BaseBranch` as separate downstream dependencies rather than
  claiming the passport removes all Ticket coupling.
- Document local-only access and validated/sanitized public fields.
- Link the feature status page to the corrected document.
- Keep docs honest: no persistence table, immutable attempt identity, rerun lineage, Notion
  lifecycle update, evidence collection, or PR packet generation in this slice.

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

Run these before returning the gap-only hardening work:

```bash
dotnet test DevAutomation.sln --no-restore --filter RunPassportContractTests
dotnet test DevAutomation.sln --no-restore --filter RunPassportEndpointTests
dotnet test DevAutomation.sln --no-restore
dotnet build DevAutomation.sln --no-restore
git diff --check
```

If `--no-restore` fails because the worktree has not restored packages, run
`dotnet restore DevAutomation.sln` once, then rerun the commands above and record the deviation.

## Definition of Done

- The existing v0 implementation is treated as the shipped baseline and promoted to v1 rather than recreated.
- `RunPassportSummaryResponse` has exact JSON, identity, path, nullability, validation, and
  public-safe failure semantics.
- Contract tests cover pristine pending, pending retry, Running, WaitingApproval, Completed,
  Failed, Cancelled, malformed links, secret/path-bearing failures, and serialized JSON.
- Endpoint tests prove 200, 404, exact wire shape, explicit nulls, and non-mutation.
- Documentation explains ticket-scoped identity, separate Ticket context, path resolution,
  local-only access, nullable-field availability, and unimplemented evidence producers.
- No persistence table, immutable attempt identity, rerun lineage, or downstream publication is
  introduced.
- Focused tests, full tests, build, and diff whitespace checks pass or any environmental blocker
  is recorded.

## Resolved Implementation Decisions

- No endpoint-test exception remains; HTTP behavior is verified with `WebApplicationFactory`.
- The ambiguous v0 `UpdatedAt` field is renamed to `LastLifecycleAt` and the breaking wire change
  is represented by `run-passport-summary/v1`.

## Risks

- **Compatibility:** consumers of v0 must opt into v1 because `updatedAt` was renamed to
  `lastLifecycleAt`; the contract version makes the incompatibility explicit.
- **False identity:** consumers may still treat the ticket key as an immutable run key unless the
  ticket-scoped boundary is repeated in code and docs.
- **False evidence:** test and residual-risk fields remain placeholders until explicit producers
  own them.
- **Naming drift:** downstream docs must use exact v1 field names and the same absence rules or
  explicitly version a new contract.
