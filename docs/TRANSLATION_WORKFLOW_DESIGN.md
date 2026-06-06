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
- "Review" button per language (opens validation results with flagged phrases highlighted; reviewer can edit and accept)
- "Send for external review" button (enabled when state is ReviewerAccepted; opens flag-confirmation dialog showing word count)
- "Cancel external review" (enabled when state is AwaitingThirdParty)
- "View history" link per language
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

### Phase 2 — Validation engine enhancement

Investigate and extend validation engine to produce `TranslationFlag` output. Update existing `TranslationValidationJob` to write flag rows.

**Estimate:** 3–5 days.

### Phase 3 — Edit page refactor

Refactor the edit page to consume the workflow service. Per-language status panel, per-language operations. Closes LEARNING_LIFECYCLE §10.9.1, §10.9.2, §10.9.4, §10.9.5, §10.9.6.

**Estimate:** 7–10 days.

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

---

## 11. Open Questions

To be resolved before or during build:

1. **Validation engine output granularity.** Does the current engine produce per-token output internally, or is phrase-level flagging new feature work? Investigation needed at start of Phase 2.
2. **Cutover plan for the wizard.** What milestone retires the old one? Is there an opt-in flag visible to admin users during the parallel phase?
3. **Stale detection.** What exactly triggers a translation transitioning to Stale? Section edits, source rewrites — needs precise specification.
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