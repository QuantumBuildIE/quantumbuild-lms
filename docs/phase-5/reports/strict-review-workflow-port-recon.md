# Strict Review Workflow Port — Recon Report

**Branch:** `transval`
**Date:** 2026-06-14
**Scope:** Characterise the old create-wizard's complete Strict reviewer workflow so an implementation chunk can port the action half of the review UI into the new learning-wizard's Validate step.

---

## Terminology

- **Old wizard** — `web/src/features/toolbox-talks/components/create-wizard/` — the original session-based wizard. Its Validate step is `steps/ValidateStep.tsx` (session-context flavour).
- **New wizard** — `web/src/features/toolbox-talks/components/learning-wizard/` + `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/validate/page.tsx`. Its Validate step is `steps/ValidateStep.tsx` (talk-context flavour) — display only, no action buttons.
- **Controller** — `src/QuantumBuild.API/Controllers/TranslationValidationController.cs`
- **Result entity** — `TranslationValidationResult` (per section, per run)

---

## 1. Decision Data Model

### Entity and Fields

`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/TranslationValidationResult.cs`

```csharp
// Reviewer decision
public ReviewerDecision ReviewerDecision { get; set; } = ReviewerDecision.Pending;
public string? EditedTranslation { get; set; }
public string? EditedSource { get; set; }
public DateTime? DecisionAt { get; set; }
public string? DecisionBy { get; set; }
```

Decision is **per-result** (one `TranslationValidationResult` row per section per run). The run-level entity (`TranslationValidationRun`) carries no aggregate decision field.

### ReviewerDecision Enum

`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Enums/ReviewerDecision.cs`

```csharp
public enum ReviewerDecision
{
    Pending  = 1,   // no decision made yet (default)
    Accepted = 2,   // reviewer accepted translation as-is
    Rejected = 3,   // reviewer rejected translation
    Edited   = 4    // reviewer edited the translation
}
```

Stored as a string (`HasConversion<string>()`) in column `ReviewerDecision varchar(50)` with `DEFAULT 'Pending'`.

### Clearing Rules

Decision fields are set/cleared in two places:

**`TranslationValidationService.ValidateSectionAsync`** (service-level re-validation):

```csharp
// Only set ReviewerDecision = Pending on FIRST creation (Id == Guid.Empty).
// Existing decisions are preserved on re-validation.
entity.ReviewerDecision = Domain.Enums.ReviewerDecision.Pending; // guarded by Id == Guid.Empty
```

So re-validation (triggered by Edit or Retry) does **not** clear `ReviewerDecision` automatically. The controller explicitly manages this transition.

**`TranslationValidationController.RetrySection`** (lines 417–419):

```csharp
// Reset to Pending while retry runs
result.ReviewerDecision = ReviewerDecision.Pending;
result.DecisionAt = null;
result.DecisionBy = null;
```

Retry explicitly resets to `Pending` after writing an audit-trail `Rejected` record first.

**`TranslationValidationController.EditSection`** (lines 339–353): Edit sets `Rejected` for the implicit rejection audit trail, then immediately overwrites with `Edited`. `DecisionAt` and `DecisionBy` are updated each time.

**Summary of clearing behaviour:**

| Trigger | Effect on ReviewerDecision |
|---|---|
| Initial section creation | Set to `Pending` (default) |
| Re-validation via service only (job re-run) | Preserved (no change) |
| `RetrySection` endpoint | Written as `Rejected` (audit), then immediately reset to `Pending` |
| `EditSection` endpoint | Written as `Rejected` (audit), then immediately set to `Edited` |
| `AcceptSection` endpoint | Set to `Accepted` |
| `RejectSection` endpoint | Set to `Rejected` |

---

## 2. Action Inventory

All per-section endpoints live under the class-level route `api/toolbox-talks/{talkId:guid}/validation`, policy `Learnings.View`. Actions that mutate decisions also require `Learnings.Admin` at the action level.

| Action | HTTP | Route suffix | Policy | Request shape | Response | State effect |
|---|---|---|---|---|---|---|
| Accept | PUT | `runs/{runId}/sections/{idx}/accept` | Learnings.Admin | No body | `200 { message }` | `ReviewerDecision = Accepted`; propagates `EditedTranslation` and `EditedSource` to `ToolboxTalkTranslation` if non-null |
| Reject | PUT | `runs/{runId}/sections/{idx}/reject` | Learnings.Admin | No body | `200 { message }` | `ReviewerDecision = Rejected` |
| Edit (translation and/or source) | PUT | `runs/{runId}/sections/{idx}/edit` | Learnings.Admin | `{ editedTranslation?: string, editedOriginalText?: string, revalidate: bool }` | `202 { message, jobId }` (with revalidate) or `200 { message }` (draft source-only save) | Records implicit `Rejected`, then sets `ReviewerDecision = Edited`; if `revalidate`, enqueues `TranslationValidationJob` for `[sectionIndex]` only |
| Retry | POST | `runs/{runId}/sections/{idx}/retry` | Learnings.Admin | No body | `202 { message, jobId }` | Records implicit `Rejected`, resets `ReviewerDecision = Pending` + nulls `DecisionAt`/`DecisionBy`, enqueues `TranslationValidationJob` for `[sectionIndex]` only |
| Get run detail | GET | `runs/{runId}` | Learnings.View | — | `ValidationRunDetailDto` direct (not `Result<T>` envelope) | Read-only |

### EditSection Request DTO

```csharp
// Inferred from controller binding
EditedTranslation: string?   // translation-only edit
EditedOriginalText: string?  // source text edit (triggers cross-language NeedsRevalidation)
Revalidate: bool             // true = enqueue re-validation; false = draft save only (source edit)
```

At least one of `EditedTranslation` or `EditedOriginalText` is required (400 if both absent).

### EditSection Concurrency Guard

If `hasSourceEdit && request.Revalidate`, the controller checks `ValidationRunStatus.Running` first and returns `409 Conflict` if a job is already running. Frontend detects 409 by message substring `'409'` and shows "Revalidation already in progress" toast.

### Source Edit Side-Effects

When `EditedOriginalText` is provided and accepted, `PropagateEditedSourceAsync` wraps the plain-text edit in `<p>` elements with HTML encoding and updates `ToolboxTalkSections[sectionIndex].Content`. All *other-language* `ToolboxTalkTranslation` rows have `NeedsRevalidation = true` set (the current language's translation is not invalidated since the edit was made in the context of that run).

---

## 3. The Full Publish Gate

### Frontend Gate — new wizard step 7 reachability

`web/src/features/toolbox-talks/components/learning-wizard/lib/stepOrder.ts` (lines 71–85):

```typescript
case 7: {
  // Sections required
  if (talk.sections.length === 0) return false;
  // Already published — no re-publish
  if (talk.status === 'Published') return false;

  const codes = parseTargetCodes(talk.targetLanguageCodes ?? null);

  // No target languages declared — English-only path, no translation gate
  if (codes.length === 0) return true;

  // Target languages declared — explicit handling of validation runs state
  const runs = validationRuns ?? [];
  if (runs.length === 0) return false; // none exist (not fetched yet, or no runs created)
  return runs.some((r) => r.status === 'Completed');
}
```

Step 7 is gated on `validationRuns` (a `ValidationRunSummary[]` fetched by `useValidationRuns(talkId)`). The gate asks: does **any** run have `status === 'Completed'`? It does **not** check section-level decisions. There is no `allSectionsDecided` equivalent in the new wizard's navigation logic.

### Old wizard gate — Validate step Continue button

`web/src/features/toolbox-talks/components/create-wizard/steps/ValidateStep.tsx` (lines 181–186):

```typescript
// All sections need a non-Pending reviewer decision, or the session is already Validated
const allSectionsDecided = mergedSections.every(
  (s) =>
    s.result?.reviewerDecision != null &&
    s.result.reviewerDecision !== 'Pending'
);
const canContinue = allSectionsDecided || session?.status === 'Validated';
```

The Continue button (`disabled={!canContinue}`, line 290) uses this gate.

### Backend Publish Guard

`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/PublishToolboxTalk/PublishToolboxTalkCommandHandler.cs` (lines 31–54):

```csharp
if (talk.Status == ToolboxTalkStatus.Published)
    return Result.Fail<PublishTalkResult>(
        "Learning is already published.", FailureCode.WorkflowInvalidState);

if (talk.Sections.Count == 0)
    return Result.Fail<PublishTalkResult>(
        "Learning must have at least one section before publishing.",
        FailureCode.WorkflowInvalidState);

// Reachability gate: if target languages are declared, at least one must have
// a completed validation run (quality gate — English-only path skips this).
var targetLanguageCodes = ParseTargetLanguageCodes(talk.TargetLanguageCodes);
if (targetLanguageCodes.Count > 0)
{
    var hasCompletedRun = await _dbContext.TranslationValidationRuns
        .AnyAsync(r => r.ToolboxTalkId == request.TalkId
                    && r.TenantId == request.TenantId
                    && r.Status == ValidationRunStatus.Completed
                    && !r.IsDeleted, ct);

    if (!hasCompletedRun)
        return Result.Fail<PublishTalkResult>(
            "At least one translation must have a completed validation run before publishing.",
            FailureCode.WorkflowInvalidState);
}
```

The backend does not check per-section `ReviewerDecision` as a publish precondition. The section-decision gate is purely a frontend UX gate in the old wizard (driven by `allSectionsDecided`). The new wizard omits it entirely.

---

## 4. Decision Persistence Rules

| Scenario | What survives | What is cleared |
|---|---|---|
| Initial validation run completes | All decisions start as `Pending` (default) | — |
| Retry a section | Audit `Rejected` is written, then `Pending` is set; `DecisionAt` and `DecisionBy` nulled | Prior acceptance or edit decision |
| Edit a section (with revalidation) | `Edited` decision persists through re-validation job | — |
| Edit a section (source-only, no revalidation) | `Edited` decision, `EditedSource` | — |
| Accept a section | `Accepted` + `DecisionAt` + `DecisionBy`; also propagates edits to translation rows | — |
| Re-validation job runs on a section (service path) | `ReviewerDecision` is **preserved** because the upsert guard `Id == Guid.Empty` is false | Scores, back-translations, outcome are overwritten |
| New validation run started (separate run entity) | Prior run's decisions are untouched (separate result rows) | — |

Key invariant from Note 15A: `TranslationValidationService.ValidateSectionAsync` only writes `ReviewerDecision = Pending` on entity creation (`Id == Guid.Empty`). On updates (retry/re-validate), it does not touch `ReviewerDecision`. The controller is responsible for setting `Pending` before enqueueing a retry.

---

## 5. Pass-State Behavior — Auto-decided or Explicit?

### Old wizard predicate

`web/src/features/toolbox-talks/components/create-wizard/steps/ValidateStep.tsx` (lines 181–185):

```typescript
const allSectionsDecided = mergedSections.every(
  (s) =>
    s.result?.reviewerDecision != null &&
    s.result.reviewerDecision !== 'Pending'
);
```

This checks **every section** regardless of outcome. A `Pass` section with `reviewerDecision === 'Pending'` **blocks** `canContinue`. Pass sections are not auto-decided. Every section — Pass, Review, and Fail — requires an explicit Accept, Edit, or Retry action from the reviewer.

### Sections with no result yet

A section with `result === null` (still pending validation) also blocks the gate because `s.result?.reviewerDecision` is `undefined`, which is `!= null` is false, so `every()` fails. The section must both have a validation result AND have a non-Pending decision.

### Verification

`ValidationSectionCard.tsx` line 181–182:

```typescript
const hasDecision =
  result?.reviewerDecision && result.reviewerDecision !== 'Pending';
```

The Accept button is shown for all sections (lines 591–608). Its visual state changes (green background) when `result.reviewerDecision === 'Accepted'` but it remains clickable. There is no auto-accept on Pass.

---

## 6. UI Surface Description

### Card structure (`ValidationSectionCard.tsx`)

Each section renders as a single rounded-border card with three zones stacked vertically:

**Zone 1 — Header (always visible, clickable to expand)**
Left-to-right: chevron icon, section label badge (`L01`, `L02` — monospace, outline), section title (truncated), optional Safety badge (orange, `ShieldAlert` icon), optional Wand2 auto-correction badge (amber, tooltip listing corrected terms), numeric score (coloured by outcome), outcome pill (`Pass`/`Review`/`Fail` — green/amber/red backgrounds), decision badge (appears when `reviewerDecision !== 'Pending'`).

Card border and background color by outcome: Pass = `border-green-200 bg-green-50/50`, Review = `border-amber-200 bg-amber-50/50`, Fail = `border-red-200 bg-red-50/50`.

**Zone 2 — Reason chips (between header and body, Review/Fail only)**
Expandable pill chips for `ReviewReasonType` values: RegistryViolation (red), GlossaryMismatch (amber), ArtefactDetected (amber), SafetyCriticalBump (purple), LowScore (gray). Clicking a chip shows an inline detail row below the chips.

**Zone 3 — Body (expanded only)**
- Side-by-side 2-column grid: Original (English) | Translation
  - In edit mode: Original shows an amber warning about formatting loss, then a `<Textarea>`; Translation shows a `<Textarea>` with `autoFocus`
  - In view mode: both are read-only `<div>` panes with `bg-muted/20 rounded-md border p-3`
- Edit action bar (only in edit mode): "Re-validate" button (`RefreshCw` icon, `size="sm"`) + "Cancel" button. "Re-validate" is disabled while `!canSubmitEdit` or `isDecisionPending`
- Glossary mismatch warning block (amber, `AlertTriangle` icon) listing each mismatch
- Back-translations 2-column grid: "Back-translation A" (labelled "Claude Haiku") | "Back-translation B" (labelled "DeepL")
- Scores row: Score A vs Original | Score B vs Original | A+B Agreement | Consensus (coloured label: Verified / Marginal / Insufficient)
- Round indicator: 3 pill dots, filled up to `roundsUsed`; safety threshold note if `isSafetyCritical`
- Critical terms: badge list (orange)
- **Action buttons** (non-readOnly, non-editing, bottom of body, after a `border-t pt-3`):
  - **Accept** (`Check` icon) — variant `default` with `bg-green-600` when already Accepted; `outline` otherwise. Disabled while `isDecisionPending`.
  - **Edit** (`Pencil` icon) — variant `outline`, calls `startEdit()`. Disabled while `isDecisionPending`.
  - **Retry** (`RefreshCw` icon, or `Loader2` spinning when pending) — variant `outline`. Disabled while `isDecisionPending`.
  - **Flag issue** (`Flag` icon, amber border/text) — only rendered when `onFlagDeviation` prop is provided AND `hasDecision` is true. Positioned `ml-auto` (right side).
- `readOnly` mode hides Accept/Edit/Retry but keeps Flag issue button.

### Decision badge appearance

`decisionBadge` map (lines 97–116):

| Decision | Label | Background/text | Icon |
|---|---|---|---|
| Accepted | "Accepted" | `bg-green-100 text-green-800` | `Check` |
| Rejected | "Rejected" | `bg-red-100 text-red-800` | `X` |
| Edited | "Edited" | `bg-blue-100 text-blue-800` | `Pencil` |

Badge appears in the header row right-most position.

### Auto-collapse on Accept

`useEffect` (lines 168–177): when `result?.reviewerDecision` transitions to `'Accepted'`, `setIsExpanded(false)` fires. Previously-Accepted sections open in collapsed state.

### Loading state during action

`isDecisionPending` (from `useMutation.isPending`) disables all three action buttons simultaneously. The Retry button shows `Loader2 animate-spin` while `isDecisionPending` is true (line 624–628).

---

## 7. Workflow Service Integration

The per-section reviewer actions (Accept, Reject, Edit, Retry) in the old wizard and the new wizard's existing validation page **do not route through `ITranslationWorkflowService`**. They call `TranslationValidationController` directly.

`ITranslationWorkflowService` is a separate workflow event log for the talk-level translation lifecycle (states: `Translating`, `AIGenerated`, `Validating`, `Validated`, `ReviewerAccepted`, `AwaitingThirdParty`, `ThirdPartyReviewed`, `AcceptedAsFinal`, `Stale`). The per-section `ReviewerDecision` fields exist on `TranslationValidationResult` rows and are managed by `TranslationValidationController` independently of the workflow service.

**Workflow service methods relevant to the validate step context (not per-section):**

- `RecordValidationCompleted` — called by `TranslationValidationJob` when all sections complete; transitions talk-language state to `Validated`
- `AcceptAsFinal` — called by `ToolboxTalksController` (not `TranslationValidationController`); transitions state to `AcceptedAsFinal`
- `InitiateExternalReview` — transitions state to `AwaitingThirdParty`; requires state `ReviewerAccepted`

The old wizard's `ValidateStep.tsx` does not call any workflow service method. All its mutations go to `TranslationValidationController`.

**Which frontend hooks call which controller:**

| Hook | Called endpoints | Controller |
|---|---|---|
| `useSessionSectionDecision` | `acceptSessionSection`, `editSessionSection`, `retrySessionSection` | `TranslationValidationController` |
| `useSectionDecision` | `acceptSection`, `editSection`, `retrySection` | `TranslationValidationController` |
| `useSessionValidationRun` | `getSessionValidationRun` | `TranslationValidationController` GET `runs/{runId}` |
| `useValidationRuns` | `getValidationRuns` | `TranslationValidationController` GET `runs` |

`acceptSessionSection`, `editSessionSection`, `retrySessionSection` and `acceptSection`, `editSection`, `retrySection` call the same backend endpoints — the "Session" variants are named for wizard context but hit the same routes.

---

## 8. Bypass Mechanics

### Old wizard bypass: `session?.status === 'Validated'`

`web/src/features/toolbox-talks/components/create-wizard/steps/ValidateStep.tsx` (line 186):

```typescript
const canContinue = allSectionsDecided || session?.status === 'Validated';
```

When `ContentCreationSession.Status === 'Validated'`, the Continue button is enabled regardless of section decision state. This is the escape hatch for sessions that were already fully validated before the reviewer UI was built, or sessions where re-entry finds the session already in terminal state.

**When does `session.status` become `'Validated'`?**

`TranslationValidationJob.cs` (line 1196) — inside the job's final per-session sweep after all runs complete:

```csharp
session.Status = ContentCreationSessionStatus.Validated;
```

This transition happens when all language validation runs are complete. It does not require reviewer decisions.

**`ContentCreationSessionStatus` enum values** (from code search):
`Draft`, `Parsing`, `Parsed`, `TranslatingValidating`, `Validated`, `GeneratingQuiz`, `QuizGenerated`, `Publishing`, `Completed`, `Abandoned`, `Failed`.

### New wizard equivalent

The new wizard's `ValidateStep.tsx` (learning-wizard) has **no** per-section decision UI and **no** `allSectionsDecided` gate. The Continue button (and step 7 reachability) is gated purely on `runs.some((r) => r.status === 'Completed')` in `stepOrder.ts`.

The new wizard uses `talk.status` from `useTalk` and `validationRuns` from `useValidationRuns`. There is no `ContentCreationSession` in the new wizard. The bypass concept maps to: step 7 is reachable when any validation run has `status === 'Completed'`, independently of reviewer decisions.

### New wizard's validate page — what it already shows

`web/src/features/toolbox-talks/components/learning-wizard/steps/ValidateStep.tsx`:

- One row per language with `RunStatusIcon` (spinner/check/alert/X), language name, pass count subtitle
- A `<Badge>` with run status/outcome label
- A "Details" `<Link>` button linking to `/admin/toolbox-talks/talks/[id]/validation/[runId]?from=wizard`
- `WorkflowSubscriber` per active run ID for SignalR live updates

There are **no** per-section cards, no Accept/Edit/Retry buttons, no `allSectionsDecided` gate, no score panels in the new wizard's validate step. The detail review happens on the linked external page (`/admin/toolbox-talks/talks/[id]/validation/[runId]`).

---

## 9. Implementation Port Checklist

What the new wizard's Validate step (`learning-wizard/steps/ValidateStep.tsx`) needs to add to match the old wizard's action half:

| # | What to add | Where | Notes |
|---|---|---|---|
| 1 | Fetch `runDetail` (full run with section results) via `useValidationRun(talkId, latestRunId)` | `ValidateStep.tsx` | Already available in `use-content-creation.ts` |
| 2 | Render `<ValidationSectionCard>` per section (one per `runDetail.results` entry) | `ValidateStep.tsx` | Card component exists, reuse from `create-wizard/steps/validate/` |
| 3 | Wire `useSectionDecision()` mutation with `accept`/`edit`/`retry` actions | `ValidateStep.tsx` | Hook already exists in `use-content-creation.ts` |
| 4 | `allSectionsDecided` gate on Continue/step 7 navigation | `stepOrder.ts` or `validate/page.tsx` | Currently step 7 gates only on `runs.some(Completed)` — no decision gate |
| 5 | Handle multi-language: one `ValidationSectionCard` list per language; tab UI for language switching | `ValidateStep.tsx` | Old wizard uses `Button` tab strip driven by `runEntries` |
| 6 | `refetchRun()` after section action (1500ms delay, same as old wizard) | `ValidateStep.tsx` | Prevents stale decision badge display |
| 7 | 409 conflict toast: "Revalidation already in progress" | `ValidateStep.tsx` | `error.message.includes('409')` check (old wizard line 161) |
| 8 | `<ValidationProgressPanel>` aggregate score/stats panel | `ValidateStep.tsx` | Component already exists in `create-wizard/steps/validate/` |
| 9 | Summary bar: `X passed · Y for review · Z failed` + "Ready to publish" label | `ValidateStep.tsx` | Straightforward from `mergedSections` stats |
| 10 | `ValidationSectionCard` needs to import `SectionValidationResult`, `ReviewerDecision`, `ReviewReason` types | Types | Already in `@/types/content-creation` |
| 11 | `defaultExpanded={section.result?.outcome === 'Review'}` prop on each card | `ValidateStep.tsx` | Auto-expands Review-outcome sections |
| 12 | `isDecisionPending` prop wired from `sectionDecision.isPending` | `ValidateStep.tsx` | Shared mutex across all cards |
| 13 | `isRunning={false}` prop (static review, not live run) | `ValidateStep.tsx` | Same as old wizard line 239 |
| 14 | No `onFlagDeviation` prop needed (wizard context; deviation flagging is a post-wizard concern) | `ValidateStep.tsx` | Optional prop — omit for now |
| 15 | `acceptSessionSection`/`editSessionSection`/`retrySessionSection` functions called via talk-context endpoints (same endpoints used by `useSectionDecision`) | `content-creation.ts` | These already exist; `useSectionDecision` is the correct hook to use in the new wizard |

---

## 10. Additional Observations

### No Reject button exists anywhere

The `RejectSection` endpoint (`PUT runs/{runId}/sections/{idx}/reject`) exists on the backend but is not called from any wizard. The `decisionBadge` map in `ValidationSectionCard.tsx` includes a `Rejected` entry (red, `X` icon) for display purposes, but `Rejected` is only set implicitly: as an audit trail write immediately before `Edited` (EditSection) or before `Pending` reset (RetrySection). The old wizard's three action buttons are Accept, Edit, and Retry — there is no standalone Reject button.

### External review is not part of the wizard

External review (`InitiateExternalReview`, `CancelExternalReview`, `SubmitExternalReview`, `DeclineExternalReview`) lives in `TranslationWorkflowPanel.tsx` and `ToolboxTalksController.cs` on the talk detail page — it is not part of either wizard's Validate step. The new wizard's `PublishStep.tsx` renders an `ExternalReviewWarningBanner` if any language is in `AwaitingThirdParty` state, but that is a warning, not a gate.

### `useSessionSectionDecision` vs `useSectionDecision`

Two identical hooks exist with different names. `useSessionSectionDecision` is used in the old wizard (session context). `useSectionDecision` is used on the standalone validation run detail page. Both call the same backend endpoints via identically-shaped functions (`acceptSessionSection`/`acceptSection` etc.). For the new wizard, `useSectionDecision` is the appropriate hook since there is no session.

### `getSessionValidationRun` vs `getValidationRun`

Similarly, two functions in `content-creation.ts` fetch the same endpoint. The new wizard should use `useValidationRun(talkId, runId)`.

### `ValidationProgressPanel` hardcodes `percentComplete={100}` in review context

In the old wizard's ValidateStep.tsx (line 220), `percentComplete` is hardcoded to `100` and `isConnected` to `false`. The live progress panel (during the translate/validate phase) uses actual SignalR data. The review panel is always static — all sections are already complete.

### Score colour thresholds in `ValidationProgressPanel` are hardcoded

In `ValidationProgressPanel.tsx`, the overall score colour is derived from hardcoded thresholds 75/60, not from the `passThreshold` prop. The `passThreshold` prop is used only for the Review count pill's badge colour logic. This is an existing inconsistency in the old wizard; the new wizard port should replicate the same behaviour.

### `mergedSections` construction in old wizard

The old wizard builds `mergedSections` by iterating `state.parsedSections` (the section list from wizard state) and matching each by `sectionIndex` against `runDetail.results`. The new wizard has `talk.sections` from `useTalk`. The merge should use `talk.sections` ordered by `sectionNumber`, with `result` matched by `sectionIndex`.

### `ValidationSectionCard` duplicate `onEdit` prop (line 71 in the file as read)

Line 71 of `ValidationSectionCard.tsx` shows a duplicate `onEdit` prop definition. This is a pre-existing artefact in the source file — the file contains two identical `onEdit` lines in the interface. The component functions correctly because TypeScript merges duplicate property definitions (last wins). Not introduced by any recent change.

### Session status `'Validated'` is the entire session-level gate

In the old wizard, `session.status === 'Validated'` bypasses `allSectionsDecided`. This status is set by the Hangfire job when all language runs complete — it does not require reviewer decisions. The new wizard has no equivalent session-level bypass because it has no session. The step 7 gate (`runs.some(Completed)`) serves the same purpose: "did the pipeline finish?" rather than "did the reviewer act on all sections?".
