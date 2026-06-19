# §26 Chunk 3 Recon — Re-parse / Regenerate All dialogs + isNavigating fix

_Date: 2026-06-19_
_Branch: transval_

---

## 1. Verification verdict

| Claim | File:line | Holds? |
|---|---|---|
| ParseStep has "intentionally not carried over" comment | `learning-wizard/steps/ParseStep.tsx:130` | Yes |
| QuizStep has "intentionally not carried over" comment | `learning-wizard/steps/QuizStep.tsx:169` | Yes |
| settings/page.tsx does not pass `isNavigating` | `learnings/[talkId]/settings/page.tsx:32-46` | Yes |
| parse/page.tsx DOES pass `isNavigating` | `learnings/[talkId]/parse/page.tsx:71` | Yes |

**No drift detected. All four claims hold.**

### Critical correction to prior recon summary

The initial recon agent summary incorrectly stated that ParseStep's Re-parse dialog needs to be added. **It has already been implemented** — fully, in the current source:

- `useState` imported (line 3)
- `AlertDialog` family imported (lines 12-20)
- `showReparseConfirm` state declared (line 62)
- `handleReparseClick` — checks `sections.length > 0`, opens dialog or fires directly (lines 116-123)
- `handleReparseConfirm` — closes dialog, calls `handleParse` (lines 125-128)
- AlertDialog JSX rendered at line 314-330 with title "Re-parse content?", description "This will discard your current sections and re-run the AI parsing. Any edits you've made to sections will be lost. Continue?", Cancel / Re-parse buttons

The "intentionally not carried over" comment at line 130 is attached to `handleContinue`'s cascade-reset behaviour — not to the Re-parse dialog itself. The dialog was added in a previous chunk.

**Revised scope for Chunk 3: 2 files, not 3.**

---

## 2. Legacy reference

### Re-parse dialog (legacy ParseStep)

Already implemented in new wizard. For record only:

| Field | Legacy value |
|---|---|
| Dialog title | "Section changes will reset downstream work" |
| Description | Conditional based on status (`QuizGenerated` vs `Validated`) |
| Confirm label | "Continue and reset" |
| Dismiss label | "Cancel" |
| Trigger condition | Only when sections edited AND status is `QuizGenerated` or `Validated` |
| Cascade behaviour | Clears quiz and downstream work |
| Destructive styling on confirm | No (default) |

New wizard simplified to "Re-parse content?" with non-conditional description (no cascade to mention at Step 2 — quiz doesn't exist yet). Matches the spirit without the complexity.

### Regenerate All dialog (legacy QuizStep)

Lines 444-457 of `create-wizard/steps/QuizStep.tsx`:

| Field | Legacy value |
|---|---|
| Dialog title | "Regenerate all questions?" |
| Description | "Are you sure? This will replace all current quiz questions with new AI-generated ones." |
| Confirm label | "Regenerate" |
| Dismiss label | "Cancel" |
| Trigger condition | Only when questions exist — `questions.length > 0` guard before opening |
| Cascade behaviour | Clears all questions, fires generation mutation |
| Destructive styling on confirm | No (default `AlertDialogAction`) |

---

## 3. Dialog placement and state recommendation

### QuizStep Regenerate All

**Inline in QuizStep component** — the Regenerate All button and `handleGenerateQuiz` handler both live in `QuizStep.tsx`. The action is step-internal, not page-shell-level (unlike Cancel, which is a navigation concern that lived in the page shell).

QuizStep already uses `useCallback` throughout. It does NOT currently use `useState` (line 3: only `useEffect`, `useRef`, `useCallback` imported). Adding `useState` is the only import change needed besides `AlertDialog`.

State variable: `showRegenerateConfirm` / `setShowRegenerateConfirm` (consistent with ParseStep's `showReparseConfirm`).

Handler: `handleRegenerateAllConfirm` — closes dialog, calls `handleGenerateQuiz`.

The existing "Regenerate All" button's `onClick` changes from `handleGenerateQuiz` → a new `handleRegenerateAllClick` (or inline `setShowRegenerateConfirm(true)`) — whichever is cleaner. Since there's no trigger condition check needed (the button is only rendered when questions exist), inline is fine: `onClick={() => setShowRegenerateConfirm(true)}`.

---

## 4. Dialog wording — Regenerate All

**Title:** `Regenerate all questions?`
(Verbatim from legacy — clear, concise, consistent.)

**Description:** `This will replace all current questions with newly AI-generated ones. Any edits you've made to questions will be lost.`
(Adapted from legacy — removes "Are you sure?" opener, adds explicit loss statement for clarity. No cascade to mention at Step 3.)

**Confirm label:** `Regenerate`
(Verbatim from legacy.)

**Dismiss label:** `Cancel`
(Verbatim from legacy.)

---

## 5. Destructive styling

**QuizStep Regenerate All confirm: non-destructive (default `AlertDialogAction`).**

Rationale:
- The legacy used default `AlertDialogAction` (no destructive className) — match it.
- Re-generating is not permanent data loss: the user can re-add questions manually or regenerate again. Compare with Cancel/Discard (draft permanently deleted) which IS destructive.
- ParseStep's Re-parse confirm also uses non-destructive `AlertDialogAction` — parallel styling is correct.

Do NOT use `className="bg-destructive text-destructive-foreground hover:bg-destructive/90"` on these dialogs. That class is for the Cancel/Discard flow only.

---

## 6. Async behaviour decision

`handleGenerateQuiz` (lines 109-117) calls `await generateMutation.mutateAsync()`. After it fires:
- `generateMutation.isPending` becomes true
- The component re-renders into an animated loading state (QuizStep renders its loading branch when `isGenerating` is true)
- A separate `useEffect` (lines 102-107) syncs questions from `generateMutation.data` when the mutation resolves

**The dialog can close immediately on confirm** — there is no need for the dialog to stay open while the mutation runs. The step's own loading UI handles the in-progress state. Use `AlertDialogAction` (which auto-closes on click via Radix primitive), call `handleGenerateQuiz()` in its `onClick`.

This differs from the Step 2 Cancel dialog (Chunk 2), where the dialog had to stay open during mutation to show "Discarding…" and handle errors. Here the error is shown in the step UI (the `generateMutation.error` render branch at line 256-274), not in the dialog. **No pending-state handling needed in the dialog itself.**

Pattern: `AlertDialogAction onClick={handleRegenerateAllConfirm}` where:

```tsx
const handleRegenerateAllConfirm = useCallback(() => {
  handleGenerateQuiz();
}, [handleGenerateQuiz]);
```

Or even simpler — `onClick={handleGenerateQuiz}` directly on `AlertDialogAction`, since it does nothing that needs a wrapper. Either is fine; the named handler is marginally clearer.

---

## 7. Trigger condition

**Regenerate All dialog: always fires when Regenerate All is clicked.**

The "Regenerate All" button (line 318-328) is inside the `// ── Questions editor ──` render branch (line 304+), which is only reached when `hasQuestions` is true. So when the button is visible, there are always questions to lose — no need for a runtime length check inside the handler.

The "Generate Quiz" button in the empty state (line 292-299) and the "Retry generation" button in the error state (line 263-272) both call `handleGenerateQuiz` directly — **do not gate these** with the dialog. The empty-state has nothing to lose; the error-state had its questions already cleared before the failed mutation.

---

## 8. isNavigating prop fix

`useStepNavigation` returns `isNavigating: updateStep.isPending` (confirmed at `hooks/useStepNavigation.ts:80`).

In `settings/page.tsx`, the hook is destructured at line 19-20 but `isNavigating` is not included, and `WizardLayout` at line 32-46 does not receive it.

**Two-part fix:**

**Line 19-20** — add `isNavigating` to destructure:
```tsx
// before:
const { reachableSteps, canGoBack, goBack, goNext, goToStep } =

// after:
const { reachableSteps, canGoBack, goBack, goNext, goToStep, isNavigating } =
```

**Line 39** — add prop to `WizardLayout`:
```tsx
// before:
      onBack={goBack}

// after:
      onBack={goBack}
      isNavigating={isNavigating}
```

That's the entire fix. `WizardLayout` already accepts the prop (confirmed in `WizardLayout.tsx`); it disables the Back button while `isNavigating` is true, preventing double-navigation during the step-save API call.

---

## 9. Sized implementation chunk

**Total files to change: 2**

### File 1: `web/src/features/toolbox-talks/components/learning-wizard/steps/QuizStep.tsx`

Changes:
1. Add `useState` to the `react` import on line 3
2. Add `AlertDialog`, `AlertDialogAction`, `AlertDialogCancel`, `AlertDialogContent`, `AlertDialogDescription`, `AlertDialogFooter`, `AlertDialogHeader`, `AlertDialogTitle` imports from `@/components/ui/alert-dialog`
3. Add `const [showRegenerateConfirm, setShowRegenerateConfirm] = useState(false);` after the existing mutation declarations (alongside where ParseStep declares its state, for consistency)
4. Change the "Regenerate All" button `onClick` at line 323 from `handleGenerateQuiz` → `() => setShowRegenerateConfirm(true)`
5. Add `AlertDialog` JSX at the end of the `hasQuestions` render branch (after the questions editor `</div>` and before the `questionsError` block, or at the very bottom of the component's JSX)

Dialog JSX:
```tsx
<AlertDialog open={showRegenerateConfirm} onOpenChange={setShowRegenerateConfirm}>
  <AlertDialogContent>
    <AlertDialogHeader>
      <AlertDialogTitle>Regenerate all questions?</AlertDialogTitle>
      <AlertDialogDescription>
        This will replace all current questions with newly AI-generated ones.
        Any edits you&apos;ve made to questions will be lost.
      </AlertDialogDescription>
    </AlertDialogHeader>
    <AlertDialogFooter>
      <AlertDialogCancel>Cancel</AlertDialogCancel>
      <AlertDialogAction onClick={handleGenerateQuiz}>
        Regenerate
      </AlertDialogAction>
    </AlertDialogFooter>
  </AlertDialogContent>
</AlertDialog>
```

Estimated diff: ~25-30 lines added

### File 2: `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/settings/page.tsx`

Changes:
1. Add `isNavigating` to the `useStepNavigation` destructure (line 19-20)
2. Add `isNavigating={isNavigating}` to `WizardLayout` props (after `onBack={goBack}`, line 39)

Estimated diff: 2 lines added

**Total estimated diff: ~27-32 lines net added across 2 files.**

---

## 10. Files read

- `BACKLOG.md`
- `docs/25/post-close-navigation-recon.md`
- `docs/26/chunk-2-fix.md`
- `docs/26/chunk-1-fix.md`
- `web/src/features/toolbox-talks/components/create-wizard/steps/ParseStep.tsx` (legacy)
- `web/src/features/toolbox-talks/components/create-wizard/steps/QuizStep.tsx` (legacy)
- `web/src/features/toolbox-talks/components/learning-wizard/steps/ParseStep.tsx`
- `web/src/features/toolbox-talks/components/learning-wizard/steps/QuizStep.tsx`
- `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/settings/page.tsx`
- `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/parse/page.tsx`
- `web/src/features/toolbox-talks/components/learning-wizard/hooks/useStepNavigation.ts`
- `web/src/features/toolbox-talks/components/learning-wizard/components/WizardLayout.tsx`
- `web/src/features/toolbox-talks/components/learning-wizard/components/DeleteDraftDialog.tsx`

---

## 11. Out of scope items flagged

- **Retry generation button** (QuizStep error state, line 263-272) — calls `handleGenerateQuiz` directly after a failed generation. No dialog needed here; form was already reset before the failed mutation, nothing to lose.
- **Generate Quiz button in empty state** (line 292-299) — same reasoning; no existing questions to lose.
- Cancel affordances on Steps 3-7 — different chunk per BACKLOG §26.
- Step 5 translate/validate gate — unrelated.
- StepIndicator navigation during in-flight ops — design decision, not in scope.
