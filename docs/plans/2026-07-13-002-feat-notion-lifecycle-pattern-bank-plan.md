---
title: Notion Lifecycle Documents and Pattern Bank - Plan
type: feat
date: 2026-07-13
topic: notion-lifecycle-pattern-bank
artifact_contract: ce-unified-plan/v1
artifact_readiness: implementation-ready
product_contract_source: linear-zza-52
execution: design
---

<!-- markdownlint-disable MD013 MD025 MD036 -->

# Notion Lifecycle Documents and Pattern Bank - Plan

## Goal Capsule

| Field | Value |
| --- | --- |
| Objective | Define the v1 Notion lifecycle document and pattern bank contract for ReplaceMe so Linear, GitHub, Run Passport, and later Langfuse evidence use one stable backlink and promotion model. |
| Product authority | Linear ZZA-52: Notion 작업 문서와 패턴 뱅크 설계. |
| Execution profile | Design/documentation contract only for this slice; no external Notion, Linear, or GitHub changes. |
| Stop conditions | Stop before implementing automation hooks that require Langfuse integration, full Run Passport persistence, or unproven idempotent Notion writes. The API/worker split and retry/DLQ dependencies are now satisfied by ZZA-59/ZZA-61. |
| Tail ownership | Later implementation tickets should use this plan as the contract and keep provider automation behind readiness and runtime gates. |

---

## Product Contract

### Summary

ReplaceMe needs a Notion surface that is useful during and after each automated development run without turning Notion into the system of record. v1 should create or update a ticket-scoped lifecycle page, expose clear backlinks to Linear, GitHub, and the Run Passport summary, and let humans promote durable learnings into a pattern bank. The lifecycle page is evidence-oriented; the pattern bank is curated memory. Langfuse traces can enrich the evidence section later, but page creation and promotion rules must work before Langfuse exists.

### Problem Frame

Run Passport v0 already reserves `notionDocumentId` and `notionDocumentUrl` and defines shared run identity fields in [`../features/run-passport.md`](../features/run-passport.md). The current document tool can create a simple ticket document, but it does not define lifecycle sections, idempotent update semantics, backlinks, or reusable pattern promotion rules. If Notion pages, PR packets, and Linear comments each invent separate naming and linking conventions, ZZA-55 and ZZA-53 will need avoidable normalization work.

The infrastructure roadmap in [`2026-07-13-001-feat-infra-foundation-roadmap-plan.md`](./2026-07-13-001-feat-infra-foundation-roadmap-plan.md) is dependency context only. ZZA-59 and ZZA-61 have since delivered the API/worker split and retry/DLQ baseline; this plan still does not duplicate OpenTelemetry Collector or Langfuse implementation details, and only states which Notion evidence slots those later capabilities may fill.

### Actors

- A1. **ReplaceMe operator.** Opens a Notion lifecycle page to understand run intent, state, evidence, and follow-up actions.
- A2. **Reviewer.** Uses Notion backlinks and pattern candidates while reviewing a GitHub PR.
- A3. **Notion lifecycle worker.** Later creates or updates ticket-scoped lifecycle pages through the document provider.
- A4. **Pattern curator.** Manually promotes stable learnings from lifecycle pages into a curated pattern bank.
- A5. **Linear/GitHub automation surfaces.** Link to the Notion page and Run Passport without becoming canonical storage for page content.

### Requirements

**Lifecycle page contract**

- R1. v1 must define one ticket/run-scoped Notion lifecycle page template with stable property names and section headings.
- R2. The lifecycle page must include Run Passport v0 identity: `contractVersion`, `runPassportId`, `runPassportUrl`, `ticketId`, `title`, `status`, and lifecycle timestamps when available.
- R3. The lifecycle page must include backlink fields for Linear issue, GitHub PR/MR, and Notion page identity without requiring any one external surface to exist first.
- R4. The lifecycle page must separate human-authored notes from generated evidence so later updates do not overwrite operator notes.
- R5. The lifecycle page must not store secrets, raw execution logs, local filesystem paths, approval payloads, or unredacted prompt/model output.

**Pattern bank contract**

- R6. v1 must define a curated pattern bank entry template separate from lifecycle pages.
- R7. Pattern bank entries must be manually promoted by a human curator; ReplaceMe may suggest candidates but must not auto-publish canonical patterns.
- R8. Pattern entries must include provenance backlinks to the source lifecycle page, Run Passport, Linear issue, and GitHub PR when present.
- R9. Pattern entries must record validation evidence, applicability, anti-patterns or limits, and residual risk before they are marked reusable.

**Automation and linking**

- R10. Lifecycle page auto-create/update must be idempotent and keyed by `runPassportId` plus provider/page id once persistence exists.
- R11. Although the API/worker split and retry/DLQ baseline from the infra roadmap are now available, lifecycle page creation should remain explicit/manual or endpoint-driven until idempotency, persistence, and redaction safety are in place; no background worker hook is required by this plan.
- R12. Linear, GitHub, and Notion backlink updates must use idempotent markers or stable sections so reruns update the existing reference instead of appending duplicates.
- R13. Run Passport remains the common read contract. Notion links should enrich Run Passport when persistence exists, but Run Passport v0 must remain usable with null Notion fields.

**Langfuse evidence boundary**

- R14. Langfuse trace URL, model/cost/duration metadata, approval wait, and outcome evidence may enrich the lifecycle page only after ZZA-60 or equivalent tracing exists.
- R15. Missing Langfuse evidence must never block lifecycle page creation, PR packet linking, Linear backlinks, or pattern promotion review.
- R16. Any Langfuse-derived content copied into Notion must pass the same redaction and allowlist posture used for logs and other external observation sinks.

### Acceptance Examples

- AE1. Given a ticket with a Linear issue and no PR yet, when the lifecycle page is created, then the page shows the Run Passport link, Linear backlink, current status, intent, and empty PR/Langfuse evidence slots.
- AE2. Given a completed ticket with a PR URL, when the lifecycle page is updated, then the GitHub backlink and validation summary are updated in place without removing human notes.
- AE3. Given a lifecycle page that contains a useful implementation lesson, when a curator promotes it, then a separate pattern bank entry is created with provenance links and a manual status of `Candidate` or `Accepted`.
- AE4. Given Langfuse is disabled or not implemented, when Notion lifecycle automation runs, then the trace section records `Not captured` or remains empty and the operation still succeeds.
- AE5. Given the same ticket is processed twice, when backlink updates are published to Linear or GitHub, then existing marked sections are updated rather than duplicated.

### Scope Boundaries

**In scope for this plan**

- v1 Notion lifecycle page template and properties.
- v1 pattern bank entry template and promotion criteria.
- Backlink rules between Notion, Linear, GitHub, and Run Passport.
- Implementation sequencing and file-level guidance for later code work.
- Langfuse enrichment slots and non-blocking evidence rules.

**Out of scope for this plan**

- Calling Notion, Linear, GitHub, or Langfuse from this design task.
- Implementing API/worker split, Kafka retry/DLQ, or Langfuse traces.
- Full Run Passport persistence or rerun lineage.
- Replacing the existing document provider abstraction with a Notion-only architecture.
- Auto-promoting pattern bank entries without curator approval.

---

## v1 Notion Templates

### Template 1: Run Lifecycle Page

**Purpose:** one page per ReplaceMe ticket/run that explains intent, current state, links, generated evidence, and human review notes.

**Creation key:** `runPassportId` from Run Passport v0. If a later persistent document table exists, it should store `{ runPassportId, provider, documentId, documentUrl }` and use it for idempotent updates.

**Recommended title format:** `ReplaceMe Run - {externalIssueKey or shortTicketId} - {title}`.

**Properties**

| Property | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- |
| `Run Passport ID` | text | `runPassportId` | Yes | Stable idempotency key, e.g. `ticket:{ticketId}`. |
| `Run Passport URL` | url/text | `runPassportUrl` | Yes | Relative API URL is acceptable until a public base URL exists. |
| `Contract Version` | select/text | `contractVersion` | Yes | Starts with `run-passport-summary/v0`. |
| `Ticket ID` | text | `ticketId` | Yes | Original ReplaceMe ticket id. |
| `Title` | title | `title` | Yes | Human title. |
| `Status` | select | `status` | Yes | Mirrors Run Passport status names. |
| `Linear Issue` | url/text | `externalIssueUrl` | No | Include key text when URL is absent. |
| `GitHub PR` | url | `pullRequestUrl` | No | Null/empty while not available. |
| `Repository` | url/text | `Ticket.RepoUrl` | Yes | Existing ticket field, not in Run Passport v0. |
| `Base Branch` | text | `Ticket.BaseBranch` | Yes | Existing ticket field. |
| `Created At` | date | `createdAt` | Yes | From ticket lifecycle. |
| `Started At` | date | `startedAt` | No | Empty until run starts. |
| `Completed At` | date | `completedAt` | No | Empty until terminal state. |
| `Notion Surface` | select | provider | Yes | `Lifecycle`. |
| `Pattern Candidates` | relation/url/text | curator | No | Links to promoted or candidate pattern entries. |

**Sections**

1. `Intent` — original ticket description and success criteria. Human edits allowed.
2. `Run Passport` — compact table of contract version, run id, relative URL, status, timestamps, failure reason if present.
3. `Backlinks` — Linear issue, GitHub PR/MR, Notion page URL, and any PR review packet link added by ZZA-55.
4. `Execution Timeline` — status changes, approval wait notes, terminal outcome. Later worker hooks may append sanitized events.
5. `Evidence` — test summary, residual risk summary, and links to detailed logs or PR checks when available. No raw logs.
6. `Langfuse Trace Evidence` — optional trace URL, model/provider, token/cost summary, duration, and outcome. Empty or `Not captured` is valid.
7. `Reviewer Notes` — human-only notes, decisions, and follow-up actions. Automation must not overwrite this section.
8. `Pattern Candidates` — generated suggestions or human notes that may be manually promoted.
9. `Appendix` — sanitized implementation notes that are useful but not canonical evidence.

### Template 2: Pattern Bank Entry

**Purpose:** reusable, curated knowledge extracted from one or more lifecycle pages. Pattern entries are not a transcript of a run; they are durable guidance.

**Creation key:** curator-controlled slug such as `pattern:{domain}:{short-name}`. Do not key canonical patterns only by a single run id because one pattern may have multiple sources.

**Recommended title format:** `Pattern - {domain} - {short name}`.

**Properties**

| Property | Type | Source | Required | Notes |
| --- | --- | --- | --- | --- |
| `Pattern ID` | text | curator | Yes | Stable slug. |
| `Status` | select | curator | Yes | `Candidate`, `Accepted`, `Deprecated`, or `Rejected`. |
| `Domain` | select/multi-select | curator | Yes | Example: `Provider`, `Agent`, `Testing`, `Docs`, `Infra`. |
| `Source Lifecycle Pages` | relation/url | Notion | Yes | At least one source before `Accepted`. |
| `Source Run Passports` | text/url list | Run Passport | Yes | Include `runPassportId` and URL. |
| `Source Linear Issues` | url/text list | Linear | No | Required when source issue exists. |
| `Source GitHub PRs` | url list | GitHub | No | Required when source PR exists. |
| `Validation Level` | select | curator | Yes | `Observed`, `Tested`, `Repeated`, `Superseded`. |
| `Last Reviewed At` | date | curator | Yes for `Accepted` | Manual review date. |
| `Owner` | person/text | curator | No | Maintainer or team. |

**Sections**

1. `Problem` — the recurring problem this pattern solves.
2. `Recommended Pattern` — concise guidance that can be reused in future tickets.
3. `When To Use` — applicability criteria.
4. `When Not To Use` — limits, anti-patterns, and counterexamples.
5. `Implementation Checklist` — specific steps or checks.
6. `Validation Evidence` — tests, review outcomes, repeated sightings, and links to source runs.
7. `Residual Risks` — known risks that remain even when following the pattern.
8. `Provenance` — Notion lifecycle pages, Run Passport references, Linear issues, and GitHub PRs.
9. `Changelog` — curator edits and deprecation notes.

### Template 3: Pattern Bank Index

**Purpose:** optional Notion parent/index page or database view that helps operators find accepted patterns by domain and status.

**Required views**

- `Accepted by Domain` — accepted entries grouped by `Domain`.
- `Candidates Needing Review` — `Candidate` entries sorted by creation date.
- `Recently Deprecated` — deprecated entries with replacement links.
- `Source Coverage` — entries with missing provenance or validation evidence.

This index is a navigation aid only. It must not replace the pattern entry provenance fields.

---

## Auto-Create vs Manual Promotion Criteria

| Decision | Default v1 behavior | Auto allowed when | Manual required when | Rationale |
| --- | --- | --- | --- | --- |
| Create lifecycle page | Manual or explicit API action by default; worker-triggered later after idempotency/persistence gates | Readiness passes, document provider is `Notion`, a ticket exists, `runPassportId` is known, and idempotency lookup finds no existing page | Provider disabled, credentials missing, operator wants no external doc, or run is exploratory/private | Lifecycle docs are operational evidence, but external writes must stay gated. |
| Update lifecycle page | Explicit endpoint/update operation by default; worker lifecycle hook later after idempotency/persistence gates | Existing page id is known and update targets generated sections only | Conflict with human-edited generated section, missing idempotency key, or redaction uncertainty | Prevent duplicate pages and protect human notes. |
| Add Linear backlink | Later idempotent comment or marked description section | Linear issue URL/key exists and the Notion page URL is available | Linear issue missing, API permission missing, or user chooses local-only mode | Linear should point to evidence but not be the page source of truth. |
| Add GitHub PR backlink | Later PR body marked section or comment | PR URL exists and Notion page URL plus Run Passport URL are available | PR not created, PR body owned by another workflow, or provider permission missing | PR reviewers need links without duplicate comments. |
| Suggest pattern candidate | Allowed as generated notes in lifecycle page | A run yields a reusable lesson with evidence and no sensitive content | Candidate contains secrets, raw prompts, one-off project specifics, or no validation | Suggestions are low-risk if clearly non-canonical. |
| Promote pattern bank entry | Never automatic | Not applicable | Always; curator must approve status, wording, applicability, and provenance | Canonical memory must be trusted and reviewed. |
| Mark pattern `Accepted` | Never automatic | Not applicable | Manual review confirms evidence, scope, and residual risks | Avoid cargo-cult patterns from one successful run. |

Promotion checklist for `Accepted` pattern entries:

- At least one source lifecycle page is linked.
- Run Passport id and URL are recorded for every source run.
- Linear and GitHub backlinks are recorded when those surfaces exist.
- Validation level is at least `Tested` for task-specific guidance or `Repeated` for broad engineering rules.
- The entry contains `When Not To Use` and `Residual Risks` sections.
- Curator confirms the content has no secrets, raw logs, local paths, or unredacted prompt/output content.

---

## Backlink Rules

### Canonical identities

| Identity | Canonical source | Notion representation | Notes |
| --- | --- | --- | --- |
| Run | Run Passport v0 | `Run Passport ID`, `Run Passport URL` | Required on lifecycle pages and pattern entries. |
| Ticket | ReplaceMe `Ticket.Id` | `Ticket ID` | Required for traceability even if Linear is absent. |
| Linear issue | Ticket external issue fields | `Linear Issue` property and `Backlinks` section | Use key and URL when available. |
| GitHub PR/MR | Ticket `PrUrl` | `GitHub PR` property and `Backlinks` section | Empty until agent produces a PR/MR. |
| Notion lifecycle page | Document provider response | `notionDocumentId`, `notionDocumentUrl` in future Run Passport enrichment | Do not require in Run Passport v0. |
| Langfuse trace | Future trace sink | Optional trace URL in `Langfuse Trace Evidence` | Enrichment only; not a required backlink. |

### Linear rules

- Add or update a single marked section/comment named `ReplaceMe Automation Evidence` when a Notion lifecycle URL exists.
- Include: Run Passport URL, Notion lifecycle URL, GitHub PR URL when available, and latest terminal status.
- Do not post raw logs, prompt output, or secret-adjacent failure details to Linear.
- If Linear is missing or disabled, lifecycle page creation still succeeds and records Linear as empty.

### GitHub rules

- ZZA-55 should add or update a single PR body section or review packet section named `ReplaceMe Review Packet`.
- Include: Run Passport URL, Notion lifecycle URL, Linear issue URL/key, validation summary, residual risk summary, and pattern candidates if curated or explicitly marked candidate.
- Do not require a Notion page to open a PR. If the Notion page is missing, include the Run Passport URL and leave Notion blank.
- If a lifecycle page is created after PR creation, update the marked section instead of adding a duplicate PR comment.

### Run Passport rules

- Run Passport remains the stable consumer contract for downstream surfaces.
- v0 currently returns null `notionDocumentId` and `notionDocumentUrl`; ZZA-52 implementation should introduce the smallest persistence needed to populate those fields without adding full rerun lineage.
- Notion page content must render from Run Passport fields where possible, not duplicate independent status names.
- Relative `runPassportUrl` is acceptable in Notion until deployment supplies a canonical external API base URL.

### Notion rules

- Every lifecycle page links back to Run Passport; Notion must not become the only way to find the run.
- Every promoted pattern entry links to its source lifecycle page and Run Passport references.
- Automation-owned sections must be clearly separated from human-owned sections.
- Repeated updates must target stable headings or block markers and must preserve `Reviewer Notes`, `When Not To Use`, and curator-written pattern guidance.

---

## Langfuse Evidence Enrichment

Langfuse is future evidence, not a prerequisite. When ZZA-60 or equivalent tracing lands, lifecycle pages may add these optional fields to `Langfuse Trace Evidence`:

| Evidence | Source | Required? | Redaction rule |
| --- | --- | --- | --- |
| Trace URL | Langfuse trace sink | No | URL only; no secret-bearing query strings. |
| Trace status/outcome | Langfuse trace metadata | No | Mirror terminal status after redaction. |
| Provider/model | Agent trace metadata | No | Allowlisted metadata only. |
| Duration and approval wait | Agent trace metadata | No | Numeric/time values only. |
| Token/cost summary | Langfuse metadata | No | Aggregate values only; no raw prompt/output. |
| Failure category | Sanitized failure mapping | No | No raw stack traces or local paths. |

Rules:

- Missing trace evidence is represented as empty or `Not captured`; it must not fail Notion creation/update.
- Trace enrichment is append/update only in the generated evidence section.
- Raw prompt/model output may be linked only if a later explicit opt-in contract allows it and redaction has been verified.
- Pattern promotion may cite Langfuse aggregate evidence, but curator review remains mandatory.

---

## Implementation Units

### U1. Document the Notion v1 contract

**Goal:** Publish a discoverable feature contract that implementers can use without rereading this plan.

**Dependencies:** This ZZA-52 design plan.

**Files:**

- Create `docs/features/notion-lifecycle-pattern-bank.md`.
- Update `docs/features/feature-status.md` to link the feature once implementation starts or the feature doc is added.
- Update `docs/README.md` only if the feature doc is created in the same change.

**Approach:** Extract the template, backlink, promotion, and Langfuse boundary sections from this plan into a feature document. Mark it as design-ready until code lands. Do not claim background automation is implemented.

**Verification:** `git diff --check`; inspect links to `run-passport.md` and this plan.

### U2. Add lifecycle document persistence and contract mapping

**Goal:** Store the Notion lifecycle page reference so Run Passport can return Notion fields and future updates are idempotent.

**Dependencies:** Run Passport v0 from [`../features/run-passport.md`](../features/run-passport.md). Does not require a background worker hook when exposed only through explicit/manual document creation.

**Files:**

- `src/DevAutomation.Core/Entities/Ticket.cs` or a new minimal document reference entity.
- `src/DevAutomation.Core/Contracts/RunPassportContracts.cs`.
- `src/DevAutomation.Core/Abstractions/IDocumentToolService.cs`.
- `src/DevAutomation.Infrastructure/DocumentTools/DocumentToolService.cs`.
- `src/DevAutomation.Infrastructure/DocumentTools/NotionDocumentToolClient.cs`.
- EF Core migration and persistence tests as needed.
- `tests/DevAutomation.Tests/RunPassportContractTests.cs`.

**Approach:** Prefer the smallest durable model that can map one lifecycle document reference to a ticket/run: provider, document id, document URL, created/updated timestamps, and `runPassportId`. Populate `notionDocumentId` and `notionDocumentUrl` in Run Passport only when provider is Notion. Do not add rerun lineage or pattern bank persistence in this unit.

**Test scenarios:**

- Existing ticket without document returns null Notion fields.
- Ticket with a Notion lifecycle document returns id and URL in Run Passport.
- Repeated creation request reuses or updates the existing lifecycle document instead of creating a duplicate.

**Verification:** Focused contract and persistence tests; `dotnet test DevAutomation.sln` when environment supports it.

### U3. Render the lifecycle page template through the document provider

**Goal:** Replace the current minimal Notion page body with the v1 lifecycle template while preserving provider abstraction.

**Dependencies:** U1 and U2.

**Files:**

- `src/DevAutomation.Core/Abstractions/IDocumentToolService.cs`.
- `src/DevAutomation.Infrastructure/DocumentTools/NotionDocumentToolClient.cs`.
- `src/DevAutomation.Infrastructure/DocumentTools/ConfluenceDocumentToolClient.cs` only if interface changes require parity.
- `src/DevAutomation.Api/Program.cs` for explicit endpoint semantics if needed.
- `tests/DevAutomation.Tests/DocumentToolServiceTests.cs` or equivalent.

**Approach:** Add a lifecycle-specific request model built from Ticket plus Run Passport summary. Render the stable properties and sections defined above. Keep `Reviewer Notes` human-owned and avoid overwriting it on update. If update-block support is not implemented yet, make create-only behavior explicit and require U4 for idempotent updates.

**Test scenarios:**

- Notion create payload contains Run Passport, Linear, GitHub, and lifecycle sections.
- Null PR and null Langfuse fields render as empty or `Not captured` without failure.
- Payload excludes raw logs, approval payloads, secrets, and local paths.

**Verification:** Unit tests against serialized Notion payload; no real Notion API call in unit tests.

### U4. Add idempotent backlink publishing after write-safety gates are available

**Goal:** Publish lifecycle-page backlinks to Linear and GitHub without duplicate comments or unsafe background coupling.

**Dependencies:** U2 and U3. The API/worker split and retry/DLQ baseline from [`2026-07-13-001-feat-infra-foundation-roadmap-plan.md`](./2026-07-13-001-feat-infra-foundation-roadmap-plan.md) are satisfied by ZZA-59/ZZA-61; automatic background publishing should now wait on idempotency, persistence, and redaction confidence.

**Files:**

- Issue tracker provider implementation for Linear backlink update.
- Repository provider implementation for GitHub PR section update.
- Worker/job orchestration files introduced by the runtime split.
- Provider tests for idempotent marker replacement.
- Documentation in `docs/features/notion-lifecycle-pattern-bank.md` and ZZA-55 PR packet docs.

**Approach:** Use stable markers (`ReplaceMe Automation Evidence`, `ReplaceMe Review Packet`) and update-in-place behavior. Missing Linear or GitHub surfaces should produce skipped status, not lifecycle failure. Keep external write failures observable and retryable through the queue behavior from the infrastructure roadmap.

**Test scenarios:**

- Existing marked Linear section/comment is replaced, not duplicated.
- Existing marked GitHub PR section is replaced, not duplicated.
- Missing PR URL skips GitHub update while Notion remains valid.
- Provider failure does not erase stored Notion document reference.

**Verification:** Unit tests with fake provider clients; integration smoke only with explicit credentials and manual approval.

### U5. Support manual pattern bank promotion

**Goal:** Let a curator create or update pattern bank entries from lifecycle page evidence without auto-publishing canonical memory.

**Dependencies:** U1 and U3. Does not require Langfuse.

**Files:**

- New contract/request types for pattern candidate and promotion if an API is added.
- Notion document provider methods for pattern bank entry creation.
- Optional explicit API endpoint for manual promotion.
- Tests for promotion request validation.
- Feature documentation and QA checklist.

**Approach:** Start with an explicit manual command or endpoint that accepts curator-approved content and provenance links. The API should validate required fields for `Accepted` status: source lifecycle page, Run Passport, validation level, residual risks, and no prohibited raw content. Candidate suggestions may remain lifecycle-page notes until curator action.

**Test scenarios:**

- `Candidate` entry can be created with source lifecycle page and Run Passport.
- `Accepted` entry without validation evidence or residual risk is rejected.
- Promotion payload containing local paths or secret-looking assignments is rejected or redacted according to the shared redaction policy.

**Verification:** Unit tests for validation and provider payload shape; manual Notion smoke only when explicitly configured.

### U6. Enrich lifecycle pages with Langfuse evidence when tracing exists

**Goal:** Add optional Langfuse trace metadata to generated evidence sections without changing lifecycle or promotion blocking behavior.

**Dependencies:** Langfuse trace integration from the infrastructure roadmap U4/ZZA-60.

**Files:**

- Agent trace sink contracts introduced by ZZA-60.
- Document lifecycle update service.
- Redaction tests covering Langfuse-derived values.
- Feature documentation.

**Approach:** Copy only allowlisted aggregate trace metadata into Notion: trace URL, outcome, model/provider, duration, approval wait, token/cost summary, and sanitized failure category. Do not copy raw prompt/output by default.

**Test scenarios:**

- Missing trace metadata leaves the section empty and update succeeds.
- Trace URL and aggregate metrics render when present.
- Secret-like values and local paths are redacted before Notion payload serialization.

**Verification:** Unit tests for redaction and missing-trace behavior; optional Langfuse smoke after ZZA-60.

---

## Dependencies and Sequencing

1. ZZA-52 design contract lands first as this plan.
2. U1 feature documentation can land before code to make the contract discoverable.
3. U2 and U3 can proceed with explicit/manual document creation because they do not require background worker hooks.
4. U4 no longer waits on the API/worker split or retry/DLQ baseline; it should wait for idempotency, persistence, and redaction confidence before automatic background publishing.
5. U5 can proceed after the lifecycle template exists, but pattern `Accepted` promotion always remains manual.
6. U6 waits for Langfuse tracing and must remain non-blocking.

Parallel guidance: ZZA-55 can use the backlink rules after this plan and U1 are available. ZZA-53 should wait for the infrastructure and backlink implementation pieces identified in the infra roadmap.

---

## Verification Contract

For this design task:

```bash
git diff --check
# Inspect the plan for local absolute workstation path markers before committing.
```

For later implementation units:

| Gate | Applies to | Done signal |
| --- | --- | --- |
| Markdown link inspection | U1 | Feature docs link to Run Passport and this plan without local absolute paths. |
| Contract tests | U2 | Run Passport maps Notion fields when a lifecycle document exists and nulls when absent. |
| Provider payload tests | U3, U5, U6 | Serialized Notion payloads include required sections and exclude prohibited raw content. |
| Idempotency tests | U2, U4 | Repeated operations update existing references/marked sections instead of duplicating pages or comments. |
| Redaction tests | U3, U5, U6 | Secrets, local paths, raw logs, and raw prompt/output are absent or redacted. |
| Manual smoke | U3, U5 | Real external writes are run only with explicit credentials and operator intent. |

---

## Definition of Done

- This plan defines v1 lifecycle page, pattern bank entry, and pattern bank index templates.
- Auto-create, update, suggestion, and manual promotion boundaries are explicit.
- Linear, GitHub, Run Passport, and Notion backlink rules are stable and idempotency-oriented.
- Langfuse evidence enrichment is optional and non-blocking.
- The plan cites the infra roadmap as dependency context without duplicating its implementation work.
- No code or external workspace systems are modified by this design task.

## Implementation-Time Unknowns

- Whether Notion should be modeled as a database with properties or as child pages under a parent page for the first implementation. The template supports either; implementation should choose the smallest shape compatible with configured workspace permissions.
- Whether storing one document reference directly on `Ticket` is enough or a separate document reference entity is needed for future Confluence parity. U2 should decide based on minimal persistence needs, not future rerun lineage.
- Whether GitHub PR backlink updates belong in ZZA-52 or ZZA-55. This plan defines the link contract; ZZA-55 may own PR packet rendering.

## Risks and Mitigations

- **Risk:** Notion becomes stale compared with Run Passport. **Mitigation:** Run Passport remains canonical for status; Notion stores links and generated snapshots that can be refreshed.
- **Risk:** Pattern bank fills with unreviewed agent guesses. **Mitigation:** only curator-approved entries can become `Accepted`; suggestions stay non-canonical.
- **Risk:** External backlinks duplicate on retries. **Mitigation:** use stable markers and idempotent update semantics.
- **Risk:** Langfuse or logs leak sensitive content into Notion. **Mitigation:** allowlisted aggregate evidence only and shared redaction before serialization.
- **Risk:** Background hooks can duplicate or lose external writes if idempotency is weak. **Mitigation:** target the separate worker boundary delivered by ZZA-59, use the retry/DLQ baseline from ZZA-61, and keep automatic hooks gated behind stable markers plus persisted document references.

<!-- markdownlint-enable MD013 MD025 MD036 -->
