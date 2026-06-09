### State machine enforcement

The service enforces what's allowed at each state. Calling `SubmitExternalReview` on a translation that's not in `AwaitingThirdParty` returns a domain error. Calling `StartTranslation` on `Accepted` without `confirmOverwrite=true` returns a "confirmation required" error.

### Hooks for v2 notifications

At every state transition, the service fires a `WorkflowNotificationTrigger` event (in-process; no listeners in v1). In v2 a `NotificationService` subscribes and dispatches emails or in-app notifications based on tenant/user preferences.

The trigger payload includes: tenant, workflow type, workflow instance ID, new state, triggering user. Generic enough to serve any workflow type, not just translation.

---

## 7. UI Surfaces

### 7.1 New Wizard (feature-flagged)

Fork-and-improved from the existing wizard. Existing steps adapted, translation+validation steps rebuilt around the workflow service.

**Translation step:**

- Per-language panel showing current state from the workflow service
- "Translate" button per language (enabled when state is Initial or Stale; requires confirm if state is Accepted)
- "Validate" button per language (enabled when state is AIGenerated)
- "Review" button per language (opens validation results with flagged phrases highlighted; reviewer edits and accepts from within this screen — Accept is not a separate panel button). Review uses a sectioned flow: per-section accept/edit/retry decisions reusing the existing wizard endpoints (PUT .../sections/{sectionIndex}/accept and siblings), with a final per-language Accept button at the bottom of the Review screen that fires AcceptAsFinal once all sections are decided.
- "Send for external review" button (enabled when state is ReviewerAccepted; opens flag-confirmation dialog showing word count). Edit-page implementation deferred to Phase 4 alongside the external participant portal — the 3c panel renders AwaitingThirdParty as a read-only state with no action button.
- "Cancel external review" (enabled when state is AwaitingThirdParty). Edit-page implementation deferred to Phase 4. Backend implementation also pending: no service method, endpoint, or state transition exists yet — InvitationStatus.Revoked is defined in the enum but never written. Phase 4 owns end-to-end.
- "View history" link per language (opens a modal showing the workflow event list for that language)
- Continue button enabled when all languages reach a terminal state (Accepted or explicitly skipped)

### 7.2 Edit Page (refactored)

Same per-language control panel as the wizard. Operations are the same. The difference: reachable for already-published learnings, not just drafts in the wizard.

The current single-button "Generate translations" is replaced by per-language operations.

### 7.3 External Participant Portal (public, token-scoped)

Reachable via the invitation link only. Minimal UI:

- Header: the learning's title (for context)
- Source content panel (read-only, source language)
- Translated content panel (editable text area), with flagged phrases visually highlighted in both panels
- "Submit" button — submits edited content
- "Decline" button — marks the invitation as cancelled

No login. No tenant data exposed beyond what's needed for the review. Token expires at the tenant-configured time.

Designed generically: the portal is "external participant" facing, not just "translation reviewer" facing. The same URL structure and token mechanism can serve future workflow types (asset audits, content approvals, etc.) with different page contents driven by `ContextType`.

---

## 8. Validation Engine Impact

The validation engine today produces aggregate scores. Under this design it must also produce **phrase-level annotations**: `TranslationFlag` rows with character offsets, severity, and reason.

Whether the engine already produces this internally or requires new feature work is to be confirmed at start of build (Phase 2).

If phrase-level output is not feasible at v1: ship with a fallback that highlights entire sections that scored below threshold. Less ideal but functional. The data model accommodates either.

---

## 9. Build Plan

### Phase 0 — Hotfix for LEARNING_LIFECYCLE §10.9.5

Add a confirmation dialog to the edit page's "Generate translations" button when existing translations are present. Closes the active data-loss bug without waiting for the workflow refactor.

**Estimate:** 2–4 hours.

**Status:** Complete (2026-06-06).

### Phase 1 — Workflow primitives + translation workflow service

Generic `WorkflowEvent`, `WorkflowReview`, `ExternalParticipantInvitation` tables. Migrations. `TranslationWorkflowService` interface and implementation consuming those tables. State machine, transitions, no UI callers yet. `TranslationFlag` table. Tenant settings addition.

**Deliverable:** the service compiles, has tests, writes its own event log. Generic primitives in place for future workflows.

**Estimate:** 7–10 days.

**Status:** Complete (2026-06-06). Commits b17c53a, 4cc4bb4, c617d80, 9f45906.

### Phase 2a — Section-level flagging (fallback shipped first)

Update `TranslationValidationJob` to write `TranslationFlag` rows at section granularity for sections scoring below threshold. One flag per qualifying section, spanning the full section text (StartOffset=0, EndOffset=length of original section text — flags are anchored to the source-language text so reviewers can read what's highlighted). Severity derived from outcome (Review → Warning, Fail → Error). Reason populated from `ReviewReasonsJson`. This is the fallback the design doc allows in §8; it ships first so the data path is honest end-to-end and Phase 3 has something to display.

**Estimate:** 1–2 days.

**Status:** Complete (2026-06-07). Commit d23bf8b.

**Known limitation:** Flag emission is not transactionally atomic with `TranslationValidationResult` save. Two gaps exist:

1. **Happy path:** `ValidateSectionAsync` saves the result before returning; the job saves the flag in a separate `SaveChangesAsync` immediately after. On job retry, a duplicate flag can be written for a section whose result saved but flag did not.

2. **Error path (added in Phase 2b.3a):** When validation throws and the catch block writes a failed-result row plus a section-level flag, the FK on `TranslationFlag.ValidationResultId` (added in Phase 2b.3a) requires the failed-result row to be persisted before the flag is constructed. The catch block therefore calls `SaveChangesAsync` twice — first to materialise the failed-result Id, then again to write the flag. If the process crashes between those two saves, the failed-result row exists without its corresponding flag.

Both gaps are bounded: one duplicate per retry per section under `AutomaticRetry(Attempts = 1)`; one missing flag per crash on the error path. Neither corrupts data. The error path is rare (validation engine exception) and the missing-flag failure mode is graceful — the section appears in the run as a failure with no flag detail. Acceptable for v1; revisit if either becomes a UX issue.

### Phase 2b — Phrase-level flagging

Emit phrase-level `TranslationFlag` rows alongside Phase 2a's section-level flags, using the existing-but-unused `WordDiffService`. Flags are sentence-level highlights in the source-language text (original), not the translated text — reviewers are source-language speakers who can't necessarily read the target language, so highlighting must be on text they can read.

**Design decisions (locked 2026-06-07, all locked decisions in §11 questions resolved):**

- **Diff input.** Compare the original section text against the *highest-scoring* of the four back-translations (BackTranslationA/B/C/D). Cheapest deterministic implementation. Design the back-translation selector so swapping to majority-vote (2-of-4 consensus) later is a single-method change.
- **Threshold for flag emission.** A contiguous run of `Insert` OR `Delete` operations of length ≥ 2 becomes one `TranslationFlag`. Single-word divergences are excluded (almost always function-word noise). Runs above 2 are capped only by section boundary — one flag per contiguous run regardless of length.
- **Reference frame for offsets.** `StartOffset` and `EndOffset` index into the **original section text** (source language). Phase 2b inherits Phase 2a's reviewer-readability principle. Once the diff identifies a divergent run, the implementation walks the original text to find the sentence containing those words and sets the offsets to span that entire sentence.
- **Granularity.** Sentence-level, not word-level. The diff identifies suspect words; the flag highlights the sentence that contains them, giving the reviewer context. Sidesteps the word-position-to-character-offset mapping problem.
- **Severity.** All phrase-level flags emit at `FlagSeverity.Warning`. Section-level flags (Phase 2a) continue to discriminate Review→Warning / Fail→Error. A reviewer sees one signal per flag — "this is worth looking at" — regardless of granularity.
- **Relationship to Phase 2a.** Additive, not replacement. Phase 2a's section-level flag continues to emit; Phase 2b's phrase-level flags are written alongside it. Same `TranslationFlag` table, multiple rows per section possible.

**Open implementation questions (to be resolved at start of implementation):**

- **Sentence detection in the source language.** What languages does the product currently support as source? If source is English-only, naive regex on `.`/`?`/`!` with a small abbreviation list suffices. If source includes CJK or Arabic, a real Unicode sentence-segmentation strategy is needed. Recon at start of implementation session should confirm.
- **`TranslationFlag.Reason` content for phrase-level flags.** Phase 2a uses `ReviewReasonsJson` aggregated. Phase 2b can be more specific — e.g., "Words missing from back-translation: 'safety equipment'." UX call; defaults to a sensible auto-generated string at implementation time.

**Known limitation (inherited from Phase 2a):** Atomicity gap on retry — flags are saved separately from `TranslationValidationResult`. On `AutomaticRetry`, duplicate flag rows can appear. Bounded to one duplicate per retry per flagged span. Phase 2b makes this slightly more visible because there are now N flags per section instead of 1, but the failure mode is identical.

**Higher-quality alternative deferred to v2.** AI-driven structured annotation (a dedicated Claude/Gemini pass returning structured JSON span annotations) is recorded in §12 as out of scope for v1. Phase 2b ships the heuristic version; v2 may replace it with AI-driven flagging if reviewer feedback shows the heuristic has too many false positives.

**Investigation outcome (2026-06-07):** Recon confirmed that no validation provider returns phrase-level annotations and no existing service produces sub-section data. `WordDiffService` exists with the right interface and is injected into `TranslationValidationService` but never called (CS9113). The `DiffOperation` type carries no position information — word-position-to-character-offset mapping must be built. No frontend infrastructure exists for rendering character-offset highlights. Phrase-level flagging is genuinely new work, not wiring-up of existing output.

**Estimate:** 5–8 days (revised from 3–5 in the original phase split; recon revealed the implementation is greenfield end-to-end, not just wiring up `WordDiffService`).

**Status:** Complete (2026-06-08). Commits d511933 (2b.1 utilities), 959951c (2b.2 job integration), 9309840 (2b.3a FK column), b181177 (2b.3b API exposure), fb14ae0 (2b.3c frontend rendering). Actual effort: ~1.5 days across two sessions, well under the 5–8 day estimate. Estimate overshot because the recon's "greenfield end-to-end" framing weighted heavily toward worst-case; in practice each chunk was a tight, focused unit (the four utilities had no external dependencies, the API extension was small, the frontend renderer kept scope tight).

### Phase 3 — Edit page refactor

Refactor the edit page to consume the workflow service. Per-language status panel, per-language operations. Closes LEARNING_LIFECYCLE §10.9.1, §10.9.2, §10.9.4, §10.9.5, §10.9.6.

Split into three chunks per the Phase 3 recon outcome (2026-06-08):

**Phase 3a — State machine guards.** Complete the `StartTranslation` / `StartValidation` guard enforcement deferred in Phase 1 (the `// TODO Phase 2: enforce state machine guard` markers in `TranslationWorkflowService`). Pure service-layer work — extends the existing `TranslationWorkflowServiceTests` integration suite. No UI or controller changes. Wiring the UI to a service that doesn't yet enforce its state machine would re-introduce §10.9.5-class issues at the controller layer.

**Status (Phase 3a):** Complete (2026-06-08). Commit 2cd8059. 26 of 26 tests passing in TranslationWorkflowServiceTests (was 10). Two new FailureCode values added: WorkflowInvalidState, WorkflowConfirmationRequired.

**Phase 3b — Backend integration.** Wires the workflow service into the edit-page command path:
- New endpoint exposing `GetState` per language for a talk (current `GET /translations` returns a thin DTO with no workflow state).
- `GenerateContentTranslationsCommandHandler` refactored to call `StartTranslation` (closes §10.9.5 and §10.9.6 backend-side; backend now enforces what the Phase 0 UI hotfix only suggests).
- `UpdateToolboxTalkCommand` extended to call `MarkStale` on languages whose translations are affected by section edits or section adds/removes (closes §10.9.1 and §10.9.2 backend-side). Automatic, not user-driven — the data really is stale; the user should not have to declare it.

**Status (Phase 3b.1):** Complete (2026-06-08). Commit 20dda5c. Wires `GenerateContentTranslationsCommandHandler` and `UpdateToolboxTalkCommandHandler` to the workflow service. Adds `Translating` transient state to `TranslationWorkflowState` enum and fills the `TranslationStarted → Translating` mapping gap. 353/353 tests passing; 13 new integration tests covering the wiring (4 in `GenerateContentTranslationsCommandHandlerTests`, 9 in `UpdateToolboxTalkCommandHandlerTests`) plus 6 new tests in `TranslationWorkflowServiceTests` (32 of 32). The symmetric `ValidationStarted → Validating` gap is captured in BACKLOG §10 and deferred from Phase 3.

**Status (Phase 3b.2):** Complete (2026-06-08). Commit c93ddfe. Wires `MissingTranslationsJob` (and transitively all five enqueue sites that dispatch it) to the workflow service via `GenerateContentTranslationsCommand.TriggeredBy = System`. Adds `triggeredBy` parameter (default `User`) to `StartTranslation`, `RecordTranslationCompleted`, and `MarkStale` on `ITranslationWorkflowService`. 357/357 tests passing; 2 new tests in `TranslationWorkflowServiceTests` covering System/User audit semantics, 2 new tests in new `MissingTranslationsJobTests` covering the job-to-workflow integration. `DailyTranslationScanJob` requires no code change — its narrow backfill purpose is preserved, and workflow events are emitted by the `MissingTranslationsJob` it dispatches. Staleness signals (`MarkStale`, `NeedsRevalidation`) remain user-driven; auto-heal scoped out per the 2026-06-08 design conversation.

**Status (Phase 3b.3):** Complete (2026-06-08). Commit 5c52db5. Adds `GET /api/toolbox-talks/{id}/translations/workflow-state` endpoint returning `List<TranslationWorkflowStateDto>` — one entry per language with an existing translation row, queried via the workflow service's `GetState` method per language. Auth: `Learnings.View`. 361/361 tests passing; 4 new endpoint tests covering 404 (talk not found), 200 with empty list (no translations yet), 200 with populated states across languages in different workflow states, and 401 (auth check). The endpoint is the data contract for Phase 3c's per-language status panel.

**Phase 3c — Frontend refactor.** Replaces `ContentTranslationPanel`'s batch-of-checkboxes UX with a per-language status panel. Each language renders: current workflow state, last validation outcome, per-language actions (translate, validate, accept, etc.). Closes §10.9.4 via UI clarity — slideshow staleness shown separately from translation staleness, so the asymmetry is no longer silent.

  **Status (Phase 3c.1):** Complete (2026-06-09). Commit f330d66.  Adds `LastValidationOutcome` (nullable `ValidationOutcome`) to `TranslationWorkflowStateDto`. `TranslationWorkflowService.GetState` populates it from the most recent completed `TranslationValidationRun` for the talk + language, ordered by `CompletedAt` descending. The field is null when no completed validation run exists for the language. Existing four Phase 3b.3 endpoint tests extended with `SeedValidationRunAsync` helper; "talk with two translations" test now asserts `lv` → Pass (seeded run) and `lt` → null (no run). 4/4 endpoint tests passing no new warnings. The DTO change is the first piece of the Phase 3c per-language panel data contract; the panel will surface this field per §9's Phase 3c description.

  **Status (Phase 3c.2):** Complete (2026-06-09). Commit 1582e3c. Adds two controller actions on `ToolboxTalksController` wrapping existing `ITranslationWorkflowService` methods: `GET /api/toolbox-talks/{id}/translations/{languageCode}/history` (auth `Learnings.View`, returns ordered `IReadOnlyList<WorkflowEventDto>`) and `POST /api/toolbox-talks/{id}/translations/{languageCode}/accept` (auth `Learnings.Manage`, returns 200 OK on success, 409 Conflict when current state is not one of Validated / ReviewerAccepted / ThirdPartyReviewed). Ten new integration tests covering 404, 401, ordering, populated and empty responses for history, plus the four source-state success paths and the invalid-state conflict for accept; 371/371 tests passing, no new warnings. Closes the backend surface for Phase 3c — the frontend in 3c.3–3c.5 now has the full endpoint set needed for the per-language panel, Review screen, and history modal.
  
**Out of scope for Phase 3:** §10.9.3 (Regenerate Slideshow destroys all slideshow translations) is a different UI surface, different endpoint, and structurally talk-level rather than per-language. It will be addressed as part of a future slideshow-operations phase alongside §10.9.7 (orphaned `ToolboxTalkSlideTranslation` rows after PDF re-extraction) and the slideshow half of §10.9.4 — those three together form a coherent unit of work that doesn't fit Phase 3's per-language framing.

**Estimate:** 7–10 days (3a: 1-2 days; 3b: 3-4 days; 3c: 3-4 days).

### Phase 4 — External participant portal

Public-facing page for token-scoped external reviews. Token issuance and verification. Email template. Submit/cancel endpoints. Generic enough to be reused by future workflow types.

**Estimate:** 5–7 days.

### Phase 5 — New parallel wizard (fork-and-improve)

Fork the existing wizard. Adapt steps that work as-is. Rebuild translation+validation steps around the workflow service. Implement the new decomposed status model and remove the session/draft-talk duplication.

**Estimate:** 14–20 days. This is the meaty piece — wizard surface is large, status/session refactor is substantial.

### Phase 6 — Cutover

When new wizard demonstrates feature parity and stability, retire the old wizard. Migration plan for any in-flight sessions.

**Estimate:** 3–5 days plus monitoring.

### Phase 7 (v2) — Notifications

Listener for the `WorkflowNotificationTrigger` events. Email and in-app notification UI. Tenant preferences.

**Estimate:** 5–7 days. Separate from v1.

### Total v1 estimate

6–8 weeks of focused work, assuming one developer.

Phase dependencies:

- Phase 0 is standalone, ship anytime
- Phase 1 is foundational; Phases 2, 3, 4, 5 depend on it
- Phase 2 should land before or with Phase 3
- Phase 3 and Phase 4 can proceed in parallel after Phase 1
- Phase 5 can start after Phase 1 but probably ships after Phase 4 (third-party feature is part of the new wizard's pitch)
- Phase 6 follows Phase 5
- Phase 7 is independent

---

## 10. Decisions Made

Confirmed in design conversation:

1. **History model:** events table (generic `WorkflowEvent`), not row versioning.
2. **Review entity model:** two related entities — `WorkflowReview` (any review act) and `ExternalParticipantInvitation` (invitation lifecycle).
3. **History granularity:** meaningful events only (state transitions, reviews, invitations — not every API call).
4. **Wizard approach:** parallel new wizard, fork-and-improved from existing, feature-flagged hidden in Production. Old wizard retires when new is proven.
5. **Wizard architectural improvements in scope:** status enum decomposition (artefact state / job state / user position), session/draft-talk duplication removal.
6. **Wizard architectural items deferred to BACKLOG:** soft-delete consistency across the broader codebase, draft-vs-published boundary as a domain concept.
7. **Generic-first design:** translation is the first workflow, but the tables and patterns are generic (WorkflowEvent, WorkflowReview, ExternalParticipantInvitation) so future workflows reuse them.
8. **External UX:** minimal. Source, flagged translation with highlights, edit box, submit. No login.
9. **Token lifetime:** tenant-configurable, default 30 days. Generic across all external-participant workflows.
10. **Word counting:** only at the external-review forward stage. Captured as `FlaggedWordCount` in invitation's `ContextPayload`. Tenant works out invoicing offline.
11. **Skip-if-exists asymmetry on other translation tables:** out of scope; flagged in BACKLOG.
12. **Notifications:** v2. Generic `WorkflowNotificationTrigger` hooks at all state transitions in v1; no listeners until v2.
13. **Slide / slideshow / subtitle / video / course translations:** out of scope. The workflow pattern may apply to them later if needed.
14. **§10.9.3 deferred from Phase 3.** Recon (2026-06-08) confirmed §10.9.3 is structurally talk-level (Regenerate Slideshow button, `POST /generate-slides`, all-languages hard-delete) and does not fit Phase 3's per-language workflow-service framing. Deferred to a future slideshow-operations phase alongside §10.9.7 and the slideshow half of §10.9.4.
15. **`MarkStale` is automatic, not user-driven.** When the admin edits a section's title or content (or adds/removes a section), `UpdateToolboxTalkCommand` will call `MarkStale` on every language whose translation is now stale. The user does not have to declare staleness — the data model knows. UI shows the resulting Stale state; user-driven re-translation is the response. (Resolves §11 question 3.)
16. **State machine guards complete before UI wiring.** Phase 1 deferred guard enforcement on `StartTranslation` / `StartValidation` to a later phase (TODO comments still in code). Phase 3a completes those guards before Phase 3b wires the service to controllers. Wiring an unguarded service to the UI would re-introduce §10.9.5-class issues at the controller layer.

---

## 11. Open Questions

To be resolved before or during build:

1. **Validation engine output granularity.** Resolved 2026-06-07: phrase-level flagging is new feature work. The engine produces only section-level scalars; no back-translation provider returns annotations; `WordDiffService` exists but is unused. Phase 2 split into 2a (section-level fallback, ships first) and 2b (phrase-level via WordDiffService). AI-annotation alternative deferred to v2 per §12.
2. **Cutover plan for the wizard.** What milestone retires the old one? Is there an opt-in flag visible to admin users during the parallel phase?
3. **Stale detection.** Resolved 2026-06-08: `MarkStale` triggers automatically from `UpdateToolboxTalkCommand` when a section's title or content is edited, or when sections are added/removed. The handler iterates the affected languages (those with existing translations) and calls `MarkStale` per language. Implementation lands in Phase 3b. See §10 decision 15.
4. **Multiple in-flight external invitations per (talk, language).** Allow? Block? Simplest model: one active invitation at a time.
5. **What if external submission contains content that re-fails validation?** Auto-re-validate? Surface? Leave to internal reviewer?
6. **WorkflowInstanceId encoding.** Resolved 2026-06-06: triple of WorkflowType + TargetEntityId + TargetEntitySubKey. For translation: Translation + ToolboxTalkId + languageCode. See TranslationWorkflowService and the workflows schema. (Commit hashes: 1a=b17c53a, 1b=4cc4bb4, 1b.5=c617d80, 1c=9f45906.)
7. **Background operation modelling.** The wizard's new model has a list of `BackgroundOperation` records. Is this a new entity, or do existing job tracking patterns (e.g., the `TranslationJobIds` column from §6.4) absorb into a generalised version?
8. **Bulk operations.** If a tenant batch-translates 50 talks, that's 50 workflow instances per language. Storage and query performance at scale.

---

## 12. Out of Scope for v1

Explicitly excluded:

- AI provider cost tracking
- Invoice tracking for third-party services
- Running totals / spending reports per tenant
- Per-language-pair pricing
- Notification dispatcher (v2)
- Slide / slideshow / subtitle / video / course translation lifecycle (separate work)
- Skip-if-exists fixes for sibling translation tables
- Stale auto-re-translation (Stale state requires explicit user action)
- Reviewer dashboards across multiple talks / multiple workflows (v2 or later)
- System-wide soft-delete consistency cleanup (BACKLOG)
- Draft-vs-published as a domain concept (BACKLOG)
- Second concrete workflow (asset lifecycle, content approval) — designed for, not built
- AI-driven structured-annotation phrase-level flagging (e.g., dedicated Claude/Gemini annotation pass with structured JSON response). Phase 2b uses heuristic word-diff flagging instead. Higher-quality AI annotation deferred pending v1 UX validation.

---

## 13. Designed-For Hooks (Future-Compatible Surfaces)

Documented so the build doesn't accidentally break them.

1. **`WorkflowEvent.WorkflowType`** — discriminator field. Adding a new workflow type means writing rows with a new value; no schema change.
2. **`WorkflowReview.InputPayload` / `OutputPayload`** — jsonb. Each workflow type writes its own payload shape. New workflows define their schema; existing tools still query by `WorkflowType`.
3. **`ExternalParticipantInvitation.ContextType` and `ContextPayload`** — same discriminator pattern. Future invitations (asset reviewers, audit attestation requesters) reuse the same table.
4. **`WorkflowNotificationTrigger`** — fires for every state transition in every workflow. v2 dispatcher subscribes; v1 has no listener but the trigger fires (writes a placeholder log entry or similar for debugging).
5. **`TenantSettings.ExternalParticipantTokenLifetimeDays`** — single setting governs all external-participant tokens, not just translation. If a future workflow needs a different lifetime, add a per-context override rather than per-workflow setting.
6. **Workflow service base class / interface.** Where mechanical, `TranslationWorkflowService` consumes a base abstraction (`WorkflowService<TState>` or similar). Future workflow services subclass it. The generic abstraction at v1 may be thin (just event-logging + review-recording helpers); deeper generality emerges with the second workflow.

---

## 14. Maintenance Discipline

This document is the source of truth for the translation workflow design and for the generic workflow pattern.

1. When code is built, this document is updated in the same PR to reflect what's actually shipped.
2. If implementation diverges from this design, the document is updated and the divergence noted with rationale.
3. If a decision is revisited, the old decision stays visible (annotated) and the new one added.
4. Open questions get resolved inline as answers emerge.
5. When a second workflow is added (asset, content approval, etc.), the document is split or extended — the generic pattern section becomes its own document, this one becomes "Translation Workflow."

---

*Document created: 2026-06-05. To be verified against actual implementation as code lands.*