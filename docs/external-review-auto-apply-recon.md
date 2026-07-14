# External Review Auto-Apply Refactor — Recon

**Date:** 2026-07-08
**Branch:** transval
**Scope:** Read-only investigation. No application files, migrations, or data modified.

---

## 1. Headline

**Refactor scope confirmed, no surprises — 8 backend + 7 frontend + 1 migration.**

The current implementation is exactly as the two prior recons (`docs/third-party-edit-not-landing-recon.md`, `docs/accept-endpoint-data-loss-recon.md`) described: `SubmitExternalReview` writes only a `WorkflowReview` row + a `WorkflowEvent`; the one method that would apply the reviewer's edits (`ConfirmExternalReview` → `PropagateExternalReviewEditsAsync`) has no controller route and is unreachable. The design correction (auto-apply on submit, no confirm step, provenance flag, cost-aware re-translate warning) is a well-bounded refactor: move the propagation logic that already exists and is already unit-tested from `ConfirmExternalReview` into `SubmitExternalReview`, add two provenance columns, add validation gates that don't exist today, and retire the dead confirm machinery. No architectural blockers found. The main judgment call is the state-machine question (§6), where this recon recommends repurposing `ThirdPartyReviewed` rather than removing it, because it minimises blast radius and keeps the "every language reaches Accepted via an explicit AcceptAsFinal click" invariant that every other path in the state machine already follows.

One nuance not in the original brief: the reviewer never supplies a name anywhere in the portal flow — only `ExternalParticipantInvitation.InvitedEmail` exists. `LastExternalReviewedBy` should be populated from the invitation's email, not a "reviewer name," since no name field is ever collected (see `web/src/app/external-review/[token]/page.tsx` — no name input anywhere in the Active-step form).

---

## 2. Backend change list

| # | File : Line | Change |
|---|---|---|
| 1 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Workflows/TranslationWorkflowService.cs:430-500` (`SubmitExternalReview`) | Insert validation gates after the state guard (line 453-456), then call the (moved-in) propagation logic when validation passes, then set `LastExternalReviewedAt`/`LastExternalReviewedBy` on the `ToolboxTalkTranslation` row, then write the `WorkflowReview` row (optionally, see §7) and `WorkflowEvent`, all before the single `SaveChangesAsync` at line 488. If validation fails, return a new `Result.Fail` (see §5) **before** any DB writes — no partial persistence. |
| 2 | `TranslationWorkflowService.cs:970-1060` (`PropagateExternalReviewEditsAsync`) | Body is reused, not rewritten. Currently reads the *persisted* `WorkflowReview.EditedContent` via a DB query (lines 976-985). Needs to be adapted to operate on the **in-flight submission** (the `editedContent` parameter `SubmitExternalReview` already has), since propagation must happen before/without a `WorkflowReview` row necessarily existing yet. Simplest change: extract the deserialize-and-apply logic (lines 995-1060, from "deserialise edit payload" onward) into a smaller helper that takes `(Guid talkId, string languageCode, string editedContent, CancellationToken ct)` directly, dropping the "load the review row" step (a-c, lines 975-993) since the caller already has the content in hand. |
| 3 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalkTranslation.cs` | Add `public DateTime? LastExternalReviewedAt { get; set; }` and `public string? LastExternalReviewedBy { get; set; }` (populate from `invitation.InvitedEmail`, not a name — see §1). Insert near `NeedsRevalidation` (line 65) since both are workflow-provenance flags on the same entity. |
| 4 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Abstractions/Workflows/ITranslationWorkflowService.cs:69` | Remove the `ConfirmExternalReview` declaration (dead per §8). |
| 5 | `TranslationWorkflowService.cs:502-532` (`ConfirmExternalReview` implementation) | Delete entirely (dead per §8). |
| 6 | `src/QuantumBuild.API/Controllers/ExternalReviewController.cs:58-92` (`Submit` action) | No route signature change needed — same `POST /{token}/submit` endpoint, same `SubmitExternalReviewRequest` body. Response handling needs a new branch: validation-gate failures should map to `400 BadRequest` (new case in the `result.ErrorCode switch`, lines 75-82) distinct from the existing `WorkflowInvalidState` → `409 Conflict` case, since a malformed submission is a client input error, not a state conflict. |
| 7 | `src/Core/QuantumBuild.Core.Application/Models/FailureCode.cs:3-15` | Add one new value, e.g. `WorkflowSubmissionInvalid`, for the validation-gate failure path (§5). Reuses the existing `Result.Fail(message, code)` factory pattern (per CLAUDE.md Note 25 — branch on `ErrorCode`, not message text). |
| 8 | `TranslationWorkflowService.cs:748-753` (`AcceptAsFinal` state guard) | **Conditional on the §6 decision.** If Option A (repurpose `ThirdPartyReviewed`) is chosen: **no change** — the guard already permits `ThirdPartyReviewed` and it stays correct because edits now land at submit-time, not at a since-removed confirm step. If Option B (remove `ThirdPartyReviewed` as a reachable state): remove `TranslationWorkflowState.ThirdPartyReviewed` from the permitted-source list (it can never be the live state at the moment `AcceptAsFinal` is called, since `SubmitExternalReview` would already have advanced past it) and update the doc comment at `ToolboxTalksController.cs:1780-1781`. |

Also touched, not counted as a separate numbered item since it's the same method as #1: the `EventTypeToState` map (`TranslationWorkflowService.cs:813-829`) — under Option A, line 821 (`ExternalReviewSubmitted => ThirdPartyReviewed`) is unchanged; under Option B, it changes to `ExternalReviewSubmitted => TranslationWorkflowState.Accepted`.

---

## 3. Frontend change list

| # | File : Line | Change |
|---|---|---|
| 1 | `web/src/features/toolbox-talks/components/TranslationWorkflowPanel.tsx:66-68` (`canReview`) | Remove `'ThirdPartyReviewed'` from the predicate — the internal `ReviewScreen` (per-section AI-translation accept/edit/retry) is meaningless for a state whose only remaining content is the reviewer's already-applied edits. Under Option A, add a distinct, lighter-weight action for this state instead (see item 4 below) rather than folding it back into `canReview`. |
| 2 | `web/src/features/toolbox-talks/components/WorkflowStateBadge.tsx:76-81` | Update the `ThirdPartyReviewed` entry's `label`/`tooltip`. Current tooltip ("External reviewer submitted; awaiting final accept") is close but implies edits are *not yet* applied — new copy should say edits are already live, e.g. "External review applied — click Accept to finalise" (Option A) or this entry is deleted outright (Option B, since the state no longer exists — see `web/src/types/workflows.ts:13` union member removal too). |
| 3 | `web/src/features/toolbox-talks/components/learning-wizard/components/WizardTranslationPanel.tsx:21-32,53` | Same `ThirdPartyReviewed` label/list update as item 2. Note: this new-wizard panel currently has **no** Send/Cancel-external-review UI at all (`canStart`, the `Validated`-only Review button) — external review is unreachable from this surface today, so this is a low-risk, cosmetic-only touch to keep the type/label maps exhaustive, not a functional change. |
| 4 | `web/src/features/toolbox-talks/components/ReviewScreen.tsx` | No longer reachable for `ThirdPartyReviewed` under either option (confirmed: `matchedState.lastValidationRunId`, consumed at `web/src/app/(authenticated)/admin/toolbox-talks/talks/[id]/translations/[languageCode]/review/page.tsx:34-39`, points at the pre-review AI validation run, unrelated to the external reviewer's edits — this was already true before the refactor, not a new gap). No code change required in this file itself; it simply stops being routed-to for this state once item 1 lands. |
| 5 | `TranslationWorkflowPanel.tsx` (new addition, no current line — insert near the existing conditional action buttons at lines 366-396) | Add a per-language "Accept" action (Option A) rendered when `row.state === 'ThirdPartyReviewed'`, calling the existing `useAcceptTranslation` hook directly from the panel row (same mutation `ReviewScreen.tsx:236-258` already uses) — avoids forcing a navigation into the irrelevant `ReviewScreen` just to click one button. |
| 6 | New component (no current file — e.g. `web/src/features/toolbox-talks/components/ExternalReviewDiffDrawer.tsx`) | Read-only diff view: AI-original vs reviewer-edited per section. Recommend a `Sheet`/drawer opened from the language row's new "View reviewer changes" button (parallel to the existing "View history" ghost-icon button at `TranslationWorkflowPanel.tsx:398-420`) rather than a separate page — this is a glance-and-close surface, not a workspace, and a drawer keeps the admin in context on the edit/detail page. Needs a new read endpoint (see §7 — depends on whether `WorkflowReview` is retained) returning per-section `{sectionIndex, originalTranslatedText, reviewerEditedText}`. |
| 7 | `TranslationWorkflowPanel.tsx:467-502` (overwrite/re-translate `AlertDialog`) | The dialog body (lines 480-484) is one fixed copy string regardless of *why* the language is in `Accepted` state. Needs to branch: if the language's `ToolboxTalkTranslation.LastExternalReviewedAt` is non-null (surfaced via a new field on `TranslationWorkflowStateDto`/`workflows.ts:19-31`), show cost-aware copy warning that re-translating discards a paid-for external review round, vs. the current generic "reviewer edits and validation results... replaced" copy for AI-only-reviewed languages. The dialog already has `overwriteLanguageCode`/`overwriteLanguageName` in component state (lines 93-94, 468-475) — add the provenance flag alongside those when `setOverwriteLanguageCode` is called at line 164, sourced from `stateByCode.get(...)` (line 105-107) once the DTO carries it. |

Also touched: `web/src/features/toolbox-talks/components/WorkflowHistoryModal.tsx:22-35` (`EVENT_TYPE_LABELS`) has a pre-existing, unrelated bug independent of this refactor — it maps `ConfirmExternalReviewAccepted`/`ConfirmExternalReviewRejected`, neither of which exists in `WorkflowEventTypes.cs` (the real constant is `ExternalReviewConfirmed`). Since `ExternalReviewConfirmed` is being retired as an event type under this refactor anyway (§8), this dead map entry can simply be deleted rather than fixed — worth folding into the same chunk that touches this file for the label copy in item 2, since it's a one-line adjacent cleanup, not new scope.

---

## 4. Data flow through `SubmitExternalReview`

### Current (verified against `TranslationWorkflowService.cs:430-500`)

```
1. Hash token, look up ExternalParticipantInvitation (IgnoreQueryFilters — no JWT)
2. Guard: invitation null → 404 / already Used → 409 / expired → 410
3. Guard: current state != AwaitingThirdParty → 409 (GetStateIgnoringTenantAsync, no tenant predicate at all)
4. invitation.Status = Used; invitation.UsedAt = now
5. context.WorkflowReviews.Add(new WorkflowReview { EditedContent = editedContent, Accepted = accepted, ... })
6. context.WorkflowEvents.Add(new WorkflowEvent { EventType = ExternalReviewSubmitted, ... })
7. await context.SaveChangesAsync(ct)   ← single atomic save, confirmed (see below)
8. Notify tenant admins (NotifyExternalReviewResponseAsync) — separate, non-transactional, self-swallowing exceptions
9. return Result.Ok()
```

Nothing in this method ever touches `ToolboxTalkTranslation.TranslatedSections`. That row is untouched until/unless a — currently nonexistent — call to `ConfirmExternalReview` happens.

### Target

```
1. Hash token, look up ExternalParticipantInvitation (unchanged)
2. Guard: invitation null / used / expired (unchanged)
3. Guard: current state != AwaitingThirdParty (unchanged)
4. NEW: Run validation gates on (editedContent, existing translation.TranslatedSections)
   → if invalid: return Result.Fail(..., FailureCode.WorkflowSubmissionInvalid) BEFORE any writes below
5. invitation.Status = Used; invitation.UsedAt = now
6. Load ToolboxTalkTranslation row (IgnoreQueryFilters, same tenant-from-invitation pattern as today)
7. Deserialise TranslatedSections, merge reviewer's edits per section index (moved-in PropagateExternalReviewEditsAsync body)
8. translation.TranslatedSections = re-serialised merged sections
9. translation.LastExternalReviewedAt = DateTime.UtcNow
10. translation.LastExternalReviewedBy = invitation.InvitedEmail
11. (§7 decision) Optionally still: context.WorkflowReviews.Add(new WorkflowReview { EditedContent = editedContent, ... }) for audit
12. context.WorkflowEvents.Add(new WorkflowEvent { EventType = ExternalReviewSubmitted, ... })
13. await context.SaveChangesAsync(ct)   ← everything above in one call, same atomicity shape as today
14. Notify tenant admins (unchanged, still non-transactional/best-effort — this was already true and is not part of this refactor's scope)
15. return Result.Ok()
```

Steps 6-10 are new writes inside the same transaction as steps 5, 11-12 — no new `SaveChangesAsync` call is introduced; everything still rides the one call at the end.

### Atomicity claim — verified, not just repeated

The prior recon's claim holds: `WorkflowReviews.Add` (line 461-472) and `WorkflowEvents.Add` (line 475-486) are both added to the same `IToolboxTalksDbContext` tracked-entity graph and flushed by the single `await context.SaveChangesAsync(ct)` at line 488. There is no try/catch around this block, no early return between the two `.Add()` calls, and no nested `SaveChangesAsync`. EF Core's `SaveChangesAsync` wraps all pending tracked changes in one implicit transaction (or an ambient explicit one, if present — none is opened here) — both rows commit or neither does. Confirmed by direct reading, not inference. The target flow preserves this: steps 6-10 (translation mutation) and 11-12 (review/event rows) all sit on the same `context` before the same terminal `SaveChangesAsync` call — no structural change to the transaction boundary, just more work happening before it fires.

### Tenant context — confirmed for the new writes too

`SubmitExternalReview` never calls `ICurrentUserService` (there is no JWT on this anonymous endpoint). The existing writes source `TenantId` from `invitation.TenantId` (lines 463, 477) — correctly stamped at invitation-creation time by an authenticated admin. The new write in step 6 above (loading `ToolboxTalkTranslation`) must follow the same `.IgnoreQueryFilters()` pattern already used everywhere else in this method (lines 436, 449-452) — there is no tenant predicate to add explicitly here since the lookup is by `(ToolboxTalkId, LanguageCode)` and the row is unique regardless of tenant scoping being bypassed; the surrounding rows (invitation, event) already establish that `invitation.TenantId` is the correct tenant for this operation, and the translation row being mutated is the one belonging to `invitation.TargetEntityId`, which was itself scoped to the correct tenant at invitation-creation time. No new `Guid.Empty` exposure — this method has no path that reads `ICurrentUserService` at all, so there's nothing for the auto-stamp interceptor (per CLAUDE.md Note 22) to clobber.

---

## 5. Validation gate recommendation

Rules, in the order they should run (fail fast, no partial writes):

1. **Non-empty submission** — `editedContent` must deserialise to a non-null, non-empty `List<ExternalReviewEditedSectionDto>`. (Today, `editedContent` can be `null` — verified by `DeclineExternalReview`'s sibling call passing `EditedContent = null`, and by `ExternalReviewControllerTests.cs:301-312` (`Submit_ActiveInvitation_AcceptedFalse_...`), which posts a non-null string but the type allows null. Under auto-apply, a null/empty submission on the **Active/accept path** should be rejected — there is nothing to propagate and no "accepted but empty" state should reach `TranslatedSections`.)
2. **Section count matches** — `edits.Count` must not just be non-zero; each `SectionIndex` must be in range `[0, sections.Count)` against the *live* `ToolboxTalkTranslation.TranslatedSections`, not just the snapshot the reviewer was shown (the portal's `GetPortalContext` sections come from `TranslationValidationResults`, a possibly-stale run — see `TranslationWorkflowService.cs:684-717`). The existing `PropagateExternalReviewEditsAsync` already has an out-of-range skip-with-warning behaviour (lines 1047-1053) — recommend promoting this from a silent per-entry skip to a **submission-level rejection** when *any* index is out of range, since silently dropping part of a reviewer's submission without telling them is worse under auto-apply (no downstream admin ever sees the raw submission to notice something was dropped, unlike today where `WorkflowReview.EditedContent` at least preserves the original for forensic reading).
3. **Each edited section's text is non-empty** — clarify semantics per the recon prompt: the reviewer's textarea is pre-populated with the AI translation on load (`external-review/[token]/page.tsx:107-112`) and is always submitted as a full replacement value, not a sparse "only what changed" payload (confirmed: `handleSubmit` at lines 130-153 maps over `editedSections`, which is initialised with every section's original text and only overwritten where the reviewer typed). **There is no "reviewer chose not to edit" null case in the current portal UI** — every section index is always present with either the original or reviewer-edited text. The validation gate should therefore require every submitted section's `translatedText` to be non-empty (a reviewer clearing a section to blank and submitting should be rejected, not silently applied as an empty section).
4. **Basic XSS/structural gate** — reject if any section's `translatedText` contains a `<script` tag (case-insensitive) or other obviously executable markup (`javascript:` URIs, `on\w+=` event-handler attributes). This is a coarse denylist, not full sanitisation — full HTML sanitisation of rich text is explicitly out of scope per the prompt and would need its own recon (what markup is actually legitimate in a translated section — the sections do carry HTML per `ToolboxTalkTranslation.TranslatedSections` XML doc: "JSON array of translated sections: [{SectionId, Title, Content}]" and `ToolboxTalkSection.Content` is described elsewhere in this codebase as HTML content).

**Where these should live:** Not a FluentValidation validator on a DTO — this codebase's FluentValidation usage (`StartTalkTranslationCommandValidator`, `UpdateToolboxTalkCommandValidator`, etc., all under `Commands/*`) is consistently applied to MediatR command objects. `SubmitExternalReview` is not a MediatR command — it's a direct `ITranslationWorkflowService` method call from `ExternalReviewController`, and its interesting rules (section count vs. the live translation, index range) need a DB read the request DTO alone can't validate. Recommend a small dedicated method, e.g. `private async Task<Result?> ValidateExternalReviewSubmissionAsync(Guid talkId, string languageCode, List<ExternalReviewEditedSectionDto> edits, CancellationToken ct)` on `TranslationWorkflowService` itself — consistent with the existing inline-guard style already used in this same class (`ValidateExplicitTenantId`, the reason-required check in `DeclineExternalReview:592-593`). If a second workflow type later needs the same shape of gate, extract to a shared `IExternalReviewValidator` at that point — premature to build the abstraction now for a single caller.

**Surfacing errors to the reviewer:** `ExternalReviewController.Submit` (lines 73-83) already has a `result.ErrorCode switch` returning typed HTTP statuses. Add one more arm: `FailureCode.WorkflowSubmissionInvalid => BadRequest(new { error = result.Errors.FirstOrDefault() })`. Frontend (`external-review/[token]/page.tsx:141-147`) already has an `else { toast.error(...) }` branch on non-200 — no new frontend plumbing needed, just a clearer error message than the current generic "Failed to submit. Please try again." (line 146) — recommend surfacing `result.Errors.FirstOrDefault()` in the toast body instead of the hardcoded string, so a genuinely malformed submission tells the reviewer *why* (e.g., "One or more sections cannot be left blank").

---

## 6. State machine: Option A vs Option B

### Option A — Repurpose `ThirdPartyReviewed` (recommended)

`ThirdPartyReviewed` stays in the enum, stays as the target of `ExternalReviewSubmitted` in `EventTypeToState` (`TranslationWorkflowService.cs:821`), but its meaning shifts from "reviewer responded, edits parked in a holding table, awaiting admin confirm" to "reviewer responded, edits already live in `TranslatedSections`, awaiting the same generic Accept-as-final click every other path uses."

**What changes:** `WorkflowStateBadge` copy (§3 item 2), `TranslationWorkflowPanel.canReview` drops this state (§3 item 1), a new lightweight Accept action replaces the old Review-button routing (§3 item 5), a new diff-view surface (§3 item 6). `AcceptAsFinal` needs **zero backend change** — it already permits `ThirdPartyReviewed` as a source state (`TranslationWorkflowService.cs:748-750`) and it already only writes an `AcceptedAsFinal` event with no data manipulation (lines 755-756) — which is now *correct* behaviour rather than the data-loss trap the second prior recon flagged, because there's nothing left to propagate at that point.

**What doesn't change:** the enum, the TS union (`workflows.ts:5-15`), every `Record<TranslationWorkflowState, ...>` exhaustive map across `WorkflowStateBadge.tsx`, `WizardTranslationPanel.tsx` — these keep compiling with zero type-level changes, only value/copy edits.

### Option B — Remove `ThirdPartyReviewed`, transition straight to `Accepted`

`SubmitExternalReview`'s success path writes an event that `EventTypeToState` maps directly to `TranslationWorkflowState.Accepted`. The enum member is deleted (or kept-but-dead if minimizing churn, but the prompt's framing implies actual removal). Every exhaustive `Record<TranslationWorkflowState, X>` in the frontend (`WorkflowStateBadge.tsx:33-94`, `WizardTranslationPanel.tsx:21-32`) loses an entry — TypeScript will force every call site to be touched (this is a feature of the removal, not a downside: the compiler finds every reference for you). `canReview`, `isComplete` (`WizardTranslationPanel.tsx:53`) drop the string literal. `AcceptAsFinal`'s permitted-state list drops `ThirdPartyReviewed` since it can genuinely never be the live state when that endpoint is called anymore.

**Trade-off:** most literally matches "no admin confirmation step" — the external review round *fully* completes unattended, with zero human click after invitation-send. But it breaks the pattern every other path in this state machine follows, where reaching `Accepted` always requires a distinct, explicit `AcceptAsFinal` call (AI-only: `Validated → [admin clicks Accept] → Accepted`; internally-reviewed: `ReviewerAccepted → [admin clicks Accept] → Accepted`). Under Option B, the external-review path alone auto-terminates, which means "Accepted" stops reliably meaning "an authorized internal user looked at this and finalised it" for languages that went through external review — that information doesn't disappear (it's recoverable from `LastExternalReviewedAt`/`LastExternalReviewedBy` plus the event log), but the *state* alone no longer tells you who actually signed off.

### Recommendation

**Option A.** It delivers everything the design brief actually asks for — auto-apply, no separate confirm-the-edits step, provenance flag, cost-aware re-translate warning — while changing the fewest surfaces (no enum change, no TS union change, no exhaustive-map churn across 3 frontend files) and while preserving the one architectural invariant (every language's route to `Accepted` goes through an explicit `AcceptAsFinal` click) that the rest of the state machine already relies on. The "no admin confirmation step" requirement is fully satisfied by removing `ConfirmExternalReview` — that was the step that gated *whether the edits count at all*; `AcceptAsFinal` under Option A confirms nothing about the edits (they're already live and irreversible without a re-translate), it's just the same generic "close out this language" click every other path already requires. If product later decides even that generic click is unwanted specifically for externally-reviewed languages, that's a much smaller follow-up change (skip `AcceptAsFinal` for this one path) than un-doing an enum removal would be.

---

## 7. `WorkflowReview` retention decision

**Recommend: keep, for audit — but this needs one behavioural change regardless of the option chosen.**

Today, `WorkflowReview.EditedContent` (for `ReviewerType.External`) is the **only** record of what the reviewer originally typed, as distinct from whatever the live `TranslatedSections` currently contains. Once auto-apply lands, `TranslatedSections` starts diverging from the reviewer's original submission the moment anyone re-translates or an internal reviewer later edits a section — at that point, `WorkflowReview.EditedContent` becomes the sole source for "what did the external reviewer actually write," which both the new diff-view (§3 item 6) and any future compliance/audit questions ("did we ship what the reviewer approved, or did someone silently change it after?") depend on.

The trade-off named in the prompt is real: keeping it means storing a full duplicate of the section content at submission time, indefinitely, for every external review round. But the storage cost is small (text-per-section, per-review-round, not per-employee) relative to what it buys — the alternative (deleting it) makes the diff-view impossible to build correctly after any subsequent edit, and removes the only artifact that could resolve a "the reviewer says they never wrote that" dispute.

**One behavioural change needed regardless:** currently `WorkflowReview` rows are written unconditionally on submission (line 461-472), even when nothing downstream ever reads them successfully (see the current bug — they're written but never read by anything user-facing). Under auto-apply, keep writing them, but only **after** the validation gates in §5 pass — a submission that fails validation should not leave a `WorkflowReview` audit row implying a review happened when it was actually rejected. This is a small structural note on the target data-flow in §4 (step 11 already reflects "optionally still" after the validation gate, not before).

---

## 8. Dead code removal — grep evidence

### `ConfirmExternalReview`

Full-repo search (excluding `bin`/`obj`) for the identifier `ConfirmExternalReview` returns exactly:

- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Abstractions/Workflows/ITranslationWorkflowService.cs:69` — interface declaration.
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Workflows/TranslationWorkflowService.cs:502-532` — implementation.
- `tests/QuantumBuild.Tests.Integration/Workflows/TranslationWorkflowServiceTests.cs` — six call sites, all test methods: `ConfirmExternalReview_WritesEvent` (line 254, calls at 264), `ConfirmExternalReview_FromInitial_ReturnsInvalidState` (line 519, call at 524), `ConfirmExternalReview_WithAcceptedTrue_PropagatesEditedSectionsIntoTranslatedSections` (line 1018, call at 1046), `ConfirmExternalReview_WithAcceptedFalse_DoesNotPropagateEdits` (line 1078, call at 1106), `ConfirmExternalReview_WithAcceptedTrueAndPartialEdits_PropagatesOnlyEditedSections` (line 1133, call at 1162), `ConfirmExternalReview_WithMalformedEditedContent_DoesNotPropagateAndDoesNotFail` (line 1181, call at 1202), `ConfirmExternalReview_WithSectionIndexOutOfRange_SkipsThatEntryButPropagatesValidOnes` (line 1228, call at 1256).

Zero matches anywhere in `src/QuantumBuild.API/Controllers/**`, zero in `web/src/**`, zero in migrations, zero in other doc files besides the two prior recons and `TRANSLATION_WORKFLOW_DESIGN.md`'s Phase 4.5a history entry (which documents it was built, not that anything calls it). **Confirmed: nothing outside its own declaration/implementation/tests calls it.** These seven test methods must be deleted (the propagation logic they exercise moves to and gets re-tested against `SubmitExternalReview` instead, per §2 item 2).

### `PropagateExternalReviewEditsAsync`

Full-repo search returns exactly one call site: `TranslationWorkflowService.cs:523` (inside `ConfirmExternalReview`, the method being deleted). The method itself (lines 970-1060) is private, so it cannot be called from anywhere else in the codebase by construction — confirmed no reflection/dynamic-invocation patterns exist in this file. **Confirmed dead once `ConfirmExternalReview` is removed** — but its *body* (§2 item 2) is not deleted, it's relocated and adapted into the new submit-time path.

### `WorkflowEventTypes.ExternalReviewConfirmed`

Referenced at: `WorkflowEventTypes.cs:12` (declaration), `TranslationWorkflowService.cs:526` (written inside `ConfirmExternalReview`, being deleted), `TranslationWorkflowService.cs:822` (`EventTypeToState` map entry — also removable once nothing ever writes this event type again). Frontend: `WorkflowHistoryModal.tsx:31-32` maps two **non-existent** constants (`ConfirmExternalReviewAccepted`/`ConfirmExternalReviewRejected` — these strings don't match `ExternalReviewConfirmed` and never did; a pre-existing, unrelated bug noted in the first prior recon's §7 item 4). Since the event type itself is being retired, this dead map entry can be deleted rather than fixed.

### `ThirdPartyReviewed` (only if Option B is chosen — see §6)

Full occurrence list, for reference if Option B is selected: `TranslationWorkflowState.cs:16` (enum member), `TranslationWorkflowService.cs:180, 248, 516, 518, 750, 752, 821` (backend guards/mappings), `ToolboxTalksController.cs:1781` (doc comment), `workflows.ts:13` (TS union), `WorkflowStateBadge.tsx:76-81` (badge config), `WizardTranslationPanel.tsx:29, 53` (label map + `isComplete` list), `TranslationWorkflowPanel.tsx:67` (`canReview` predicate). Every one of these needs a touch under Option B; under Option A (recommended), all but the two test-assertion sites below are either unchanged or receive copy-only edits.

Test-level references that assert on `ThirdPartyReviewed` and need updating **regardless of which option is chosen** (the copy/meaning changes even under Option A):
- `tests/QuantumBuild.Tests.Integration/ToolboxTalks/ToolboxTalksControllerWorkflowActionsTests.cs:182-197` (`AcceptAsFinal_FromThirdPartyReviewedState_Returns200`) — under Option A this test should additionally assert `TranslatedSections` reflects the reviewer's edits after the earlier submit step (it currently only asserts `200`, per the prior recon's finding that nothing checks the data-loss consequence); under Option B this test is deleted (the state can no longer be seeded as a precondition).
- `tests/QuantumBuild.Tests.Integration/ExternalReview/ExternalReviewControllerTests.cs:280-297` (`Submit_ActiveInvitation_AcceptedTrue_Returns200AndTransitionsToThirdPartyReviewed`) — under Option A, extend to assert the translation's sections actually changed and `LastExternalReviewedAt`/`By` are populated; under Option B, rename and change the assertion to `Accepted`.

---

## 9. Migration shape

New columns on `ToolboxTalkTranslation` (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalkTranslation.cs`):

```csharp
public DateTime? LastExternalReviewedAt { get; set; }
public string? LastExternalReviewedBy { get; set; }
```

Both nullable — the overwhelming majority of existing/future `ToolboxTalkTranslation` rows will never go through external review at all, so a non-null default would be meaningless. No default value needed (nullable columns default to `NULL`). No backfill needed — this is pre-Production (per the prompt) and there is no existing data to backfill retroactively from `WorkflowReview` rows (that historical linkage would require joining on `TargetEntityId`/`TargetEntitySubKey`, doable if ever wanted later, but out of scope here since the prompt explicitly says no data migration concerns).

Per CLAUDE.md Note 28: generate via `dotnet ef migrations add AddExternalReviewProvenanceToTranslations --project ../Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure` from `src/QuantumBuild.API`, and verify both `<Name>.cs` and `<Name>.Designer.cs` are produced — do not hand-write the migration.

No new table needed. `WorkflowReview` (kept per §7) and `ExternalParticipantInvitation` already exist and need no schema change — `InvitedEmail` (already on `ExternalParticipantInvitation.cs:11`) is the source for the new `LastExternalReviewedBy` column, not a new field.

---

## 10. Recommended implementation chunk breakdown

**Chunk 1 — Backend core: auto-apply + provenance + validation gates.**
Migration (§9). Move propagation logic into `SubmitExternalReview`, add validation gates (§5), add the new `FailureCode`, wire the controller's new error branch. Delete `ConfirmExternalReview` (interface + implementation) and its seven tests; rewrite/extend the two tests named in §8 to assert the new behaviour. This chunk is self-contained and independently testable — no frontend or state-machine-copy dependency yet, since `AcceptAsFinal` needs zero change under the recommended Option A.

**Chunk 2 — State machine cleanup (Option A specifics) + WorkflowReview audit-write ordering.**
Depends on Chunk 1 existing (validation gates must exist before "only write `WorkflowReview` after validation passes" can be enforced — §7). Small: reorder the `WorkflowReview.Add` call in `SubmitExternalReview` to after the gate, remove the dead `ExternalReviewConfirmed` event type and its `EventTypeToState` entry (§8), update the `ToolboxTalksController.cs:1780-1781` doc comment. If Option B is chosen instead of the recommendation, this chunk becomes the enum-removal chunk and grows substantially (every file in the Option B list in §6/§8) — recommend deciding A vs. B before scoping this chunk's estimate.

**Chunk 3 — Frontend: badge copy, panel actions, re-translate warning.**
Depends on Chunk 1 (needs `LastExternalReviewedAt`/`By` exposed on `TranslationWorkflowStateDto` — a small backend addition bundled into this chunk's start, or tacked onto the end of Chunk 1). Covers §3 items 1, 2, 3, 5, 7: badge copy, `canReview` predicate change, new per-row Accept action, re-translate dialog branching. Independently shippable once Chunk 1's data contract exists.

**Chunk 4 — Diff view.**
Depends on the §7 decision (needs `WorkflowReview.EditedContent` as its data source) and a new read endpoint exposing it per-section alongside the current `TranslatedSections` content for comparison. Lowest urgency of the four — the auto-apply behaviour is fully functional and correct without it; this chunk is purely the "let the admin glance at what changed" affordance. Can ship last, or be deferred to a fast-follow if timeline is tight.

Dependency order: **1 → 2, 1 → 3 (parallel-capable after 1) → 4 (last, needs 1's data model settled)**.
