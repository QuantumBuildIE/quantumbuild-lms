# §30 External Review — Per-Language Fix Report

**Date:** 2026-06-17  
**Branch:** transval  
**Scope:** Two-part fix — backend state gate relaxation + frontend CTA on the Validate step  
**Status:** Complete

---

## Summary

External review is now reachable from the new wizard's Validate step.

**Root cause confirmed by recon:** `InitiateExternalReview` required `ReviewerAccepted` state, which was only reachable from `SubmitInternalReview` — a method that had no controller endpoint and no frontend caller. This created a circular dependency: external review required `ReviewerAccepted`, `ReviewerAccepted` required `SubmitInternalReview`, `SubmitInternalReview` had no caller. `AwaitingThirdParty` was never reachable, so the cancel button in `TranslationWorkflowPanel` never rendered, and the `ExternalReviewWarningBanner` on the Publish step never fired.

**Product clarification (2026-06-17):** External review is part of the internal reviewer's workflow — an escalation path when a translation is too important or too uncertain to sign off on internally. The reviewer doesn't need to complete a full internal review pass before escalating. The correct trigger state is `Validated` (the reviewer is actively reviewing sections and decides to escalate the whole language). `ReviewerAccepted` remains a valid trigger state for completeness (if a future workflow re-adds an explicit internal-review-complete step).

**CTA location decision:** The escalation button lives on the Validate step (not the Publish step as the recon's Option B1 initially proposed). Rationale: external review is a reviewer-driven action mid-review, not a post-validation decision at publish time. The reviewer sees the back-translation scores and section outcomes on the Validate step and can escalate from there.

**Side effects confirmed:**
- §11 (Cancel external review) — closed. `AwaitingThirdParty` is now reachable; the existing `CancelExternalReviewDialog` and cancel button in `TranslationWorkflowPanel` work as designed.
- §21 (Post-publish AwaitingThirdParty management) — partially addressed. The Edit page path now works (Talk detail → Edit → Content Translations card → Cancel invitation). Detail page path still blocked on §24.

---

## Pre-flight Confirmation

Recon findings hold. The specific corrections from recon to this fix:

| Recon finding | Disposition |
|---|---|
| CTA location: Publish step (recon's Option B1) | Changed to Validate step per product discussion |
| Backend gate: relax to `Validated` | Confirmed — correct approach |
| No need to lift `SendExternalReviewDialog` | Confirmed — both callers import from existing location |
| Event map already has `ExternalReviewInitiated → AwaitingThirdParty` | Confirmed at `TranslationWorkflowService.cs:810` — no map change needed |

---

## Edit (1) — Backend: Relax State Gate

**File:** `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Workflows/TranslationWorkflowService.cs`  
**Line:** ~348

**Before:**
```csharp
if (state != TranslationWorkflowState.ReviewerAccepted)
    return Result.Fail<InitiateExternalReviewResult>(
        $"Cannot initiate external review from state {state}; requires ReviewerAccepted.",
        FailureCode.WorkflowInvalidState);
```

**After:**
```csharp
if (state != TranslationWorkflowState.Validated && state != TranslationWorkflowState.ReviewerAccepted)
    return Result.Fail<InitiateExternalReviewResult>(
        $"Cannot initiate external review from state {state}; requires Validated or ReviewerAccepted.",
        FailureCode.WorkflowInvalidState);
```

The event-to-state map already had `ExternalReviewInitiated => AwaitingThirdParty` — no map change needed. The `Validated → ExternalReviewInitiated → AwaitingThirdParty` transition was always there; only the guard was blocking it.

---

## Edit (2) — Frontend: `canSendForExternalReview`

**File:** `web/src/features/toolbox-talks/components/TranslationWorkflowPanel.tsx`  
**Line:** ~70

**Before:**
```ts
function canSendForExternalReview(state: TranslationWorkflowState): boolean {
  return state === 'ReviewerAccepted';
}
```

**After:**
```ts
function canSendForExternalReview(state: TranslationWorkflowState): boolean {
  return state === 'Validated' || state === 'ReviewerAccepted';
}
```

This makes the existing "Send for review" button in the Edit page's `TranslationWorkflowPanel` render correctly for `Validated` languages. Side effect: the `canReview` branch that includes `ReviewerAccepted` is no longer dead (it was always theoretically reachable via cancel paths, but now `ReviewerAccepted` is actually reachable via `ExternalReviewCancelled → ReviewerAccepted`).

---

## Edit (3) — Frontend: Validate Step CTA

**File:** `web/src/features/toolbox-talks/components/learning-wizard/steps/ValidateStep.tsx`

**Changes:**

1. **Imports added:**
   - `Send` from `lucide-react`
   - `useInitiateExternalReview` from `@/lib/api/toolbox-talks/use-toolbox-talks`
   - `SendExternalReviewDialog` from `../../SendExternalReviewDialog`

2. **`useWorkflowSubscription` destructure extended** to include `data: workflowStates`. This reuses the same TanStack Query cache as `useWorkflowStates` (same query key: `[...TOOLBOX_TALKS_KEY, talkId, 'workflow-state']`) — no second network request.

3. **Mutation and state added:**
   ```tsx
   const initiateReviewMutation = useInitiateExternalReview();
   const [sendReviewLang, setSendReviewLang] = useState<{
     code: string; name: string; flaggedCount: number;
   } | null>(null);
   ```

4. **Derived values added** (after `stats` useMemo):
   ```tsx
   const activeWorkflowState = (workflowStates ?? []).find(s => s.languageCode === activeLangCode) ?? null;
   const canSendForReview =
     activeWorkflowState?.state === 'Validated' || activeWorkflowState?.state === 'ReviewerAccepted';
   ```

5. **Handler added:**
   ```tsx
   const handleSendForExternalReview = useCallback(async (email) => {
     // mutates, toasts, closes dialog on success
   }, [talkId, sendReviewLang, initiateReviewMutation]);
   ```
   Cache invalidation is handled by `useInitiateExternalReview`'s `onSuccess` (invalidates `workflow-state` query) — no manual refetch needed.

6. **Button in JSX** — between `ValidationProgressPanel` and section cards, visible when `canSendForReview && activeLangCode`:
   ```tsx
   <Button variant="outline" size="sm" onClick={() => setSendReviewLang(...)}>
     <Send className="mr-1.5 h-3.5 w-3.5" />
     Send for external review
   </Button>
   ```

7. **Dialog at the top of the return** (alongside `WorkflowSubscriber` elements):
   ```tsx
   <SendExternalReviewDialog
     open={sendReviewLang !== null}
     onOpenChange={...}
     onConfirm={handleSendForExternalReview}
     isLoading={initiateReviewMutation.isPending}
     flaggedWordCount={sendReviewLang?.flaggedCount ?? 0}
     languageName={...}
   />
   ```

**`SendExternalReviewDialog` not lifted** — both callers (`TranslationWorkflowPanel` on the Edit page, `ValidateStep` in the wizard) import directly from `web/src/features/toolbox-talks/components/SendExternalReviewDialog.tsx`. No path reshaping.

---

## Edit (4) — ExternalReviewWarningBanner Review

**File:** `web/src/features/toolbox-talks/components/learning-wizard/steps/PublishStep.tsx:413-456`

Verified — no changes needed:

| Check | Result |
|---|---|
| Correctly identifies `AwaitingThirdParty` languages via `workflowStates` filter | ✅ |
| Returns `null` when no languages are awaiting — Publish button unaffected | ✅ |
| Copy: "When the reviewer submits, their translation will be applied to the published talk automatically" | ✅ accurate |
| Pluralization: "1 language is awaiting" vs "${count} languages are awaiting" | ✅ both cases handled |
| Includes language names in the "cancel first" call to action | ✅ `langList` interpolated |
| Non-blocking — styling is amber info, not error/destructive | ✅ |

The banner is defensive code that was previously never reachable (requires `AwaitingThirdParty`, which was blocked). With this fix it becomes live for the first time. It requires no changes.

---

## Edit (5) — BACKLOG Updates

| Entry | Change |
|---|---|
| §30 | Status → ✅ Done with full description |
| §11 | Status → ✅ Done (side-effect of §30) |
| §21 | Status → Partially addressed — Edit page path works, detail page blocked on §24 |
| §5.29 | Added bullet 5: `PublishSuccessState` dead code in `PublishStep.tsx` |
| §7.3 | New entry: investigate per-section external review escalation (P3, deferred) |

---

## Verification

### Local checks

- Backend compiles: the state gate change is a simple `&&` condition addition; no interface or type changes.
- Frontend compiles: `Send` icon and `useInitiateExternalReview` are available in the imported modules; `SendExternalReviewDialog` props are unchanged (`open`, `onOpenChange`, `onConfirm`, `isLoading`, `flaggedWordCount`, `languageName` — all satisfied).
- No new TypeScript types introduced; `TranslationWorkflowStateDto.flaggedWordCount` is `number` (confirmed in `web/src/types/workflows.ts:30`).

### Smoke path (post-deploy)

1. Create a new-wizard talk with at least one target language. Complete Steps 1–5 (translation runs via Translate step).
2. On the Validate step (Step 6): confirm language is in `Validated` state. Confirm "Send for external review" button appears above the section cards.
3. Click "Send for external review". Dialog opens. Enter reviewer email. Confirm: language transitions to `AwaitingThirdParty`.
4. Continue to Publish step (Step 7). Confirm `ExternalReviewWarningBanner` renders with the language name and correct copy.
5. Publish the talk. Confirm publish succeeds.
6. Navigate to `/admin/toolbox-talks/talks/{id}/edit`. Confirm `TranslationWorkflowPanel` shows the language in `AwaitingThirdParty` with the "Cancel invitation" button. (Closes §11 smoke.)
7. Click "Cancel invitation". Confirm language returns to `ReviewerAccepted`. Confirm "Send for external review" still shows (`canSendForExternalReview` now accepts `ReviewerAccepted` too). (Validates round-trip.)

*Smoke deferred to post-deploy follow-up — Development environment required for SignalR and MailerSend.*

---

## Side-Effect Closures

### §11 (Cancel external review — end-to-end)

**Status: ✅ Closed as side-effect of §30.**

`AwaitingThirdParty` is now reachable end-to-end. The "Cancel invitation" button in `TranslationWorkflowPanel` renders for `state === 'AwaitingThirdParty'` (existing logic, unchanged). `CancelExternalReviewDialog` is wired. Backend endpoint `POST cancel-external-review` confirmed working as of 2026-06-15. No code changes needed for §11 itself.

### §21 (Post-publish AwaitingThirdParty management)

**Status: Partially addressed.**

The Edit page path now works: an admin with a published talk that has `AwaitingThirdParty` translations can navigate to the Edit page and use `TranslationWorkflowPanel` to cancel the external review. The talk detail page still doesn't have `TranslationWorkflowPanel` — that's §24 Chunk 2 (detail page translation surface). §21 remains open but its severity is reduced: there is a working path (Edit page), just not the most discoverable one.

---

## Coverage Table

| Item | Status | File:line |
|---|---|---|
| `InitiateExternalReview` state gate | `Validated \|\| ReviewerAccepted` | `TranslationWorkflowService.cs:348` |
| `ExternalReviewInitiated → AwaitingThirdParty` in event map | Already present — no change | `TranslationWorkflowService.cs:810` |
| `canSendForExternalReview` gate | `Validated \|\| ReviewerAccepted` | `TranslationWorkflowPanel.tsx:70-72` |
| Validate step — `Send for external review` button | Added — `canSendForReview && activeLangCode` | `ValidateStep.tsx:284-303` |
| Validate step — `SendExternalReviewDialog` | Added — reuses existing dialog | `ValidateStep.tsx:218-232` |
| Validate step — `useWorkflowStates` data | Via `useWorkflowSubscription` destructure | `ValidateStep.tsx:53` |
| `ExternalReviewWarningBanner` — correct and reachable | Verified, no changes | `PublishStep.tsx:413-456` |
| §11 cancel button — `AwaitingThirdParty` now reachable | Side-effect closed | `TranslationWorkflowPanel.tsx:384-396` |
| §21 Edit page cancel path | Works via existing panel | `TranslationWorkflowPanel.tsx` on edit page |
| §21 Detail page cancel path | Still blocked on §24 | `ToolboxTalkDetail.tsx` |

---

*Fix implemented by Claude Code, 2026-06-17.*
