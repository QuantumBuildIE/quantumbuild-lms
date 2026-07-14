# §26 Chunk 3 Fix — Regenerate All confirmation dialog + settings nav prop

_Date: 2026-06-19_
_Branch: transval_
_Author: Claude Code_
_Based on recon: `docs/26/chunk-3-recon.md`_

---

## Summary of changes

Two files modified (plus BACKLOG.md scope correction already in working tree). ~35 lines net added.

| File | Change |
|---|---|
| `QuizStep.tsx` | Added `useState`, AlertDialog imports; added `showRegenerateConfirm` state; changed Regenerate All onClick to open dialog; added AlertDialog JSX at end of questions editor return |
| `settings/page.tsx` | Added `isNavigating` to `useStepNavigation` destructure; passed `isNavigating` prop to `WizardLayout` |
| `BACKLOG.md` | Scope correction (pre-existing modified file — the Re-parse dialog is already shipped, Chunk 3 scope corrected to 2 items not 3) |

---

## Test results

### `npx tsc --noEmit`

```
(no output)
```

**Clean — zero errors, zero warnings.**

### `npm run test`

```
 RUN  v4.1.9

 Test Files  3 passed (3)
      Tests  15 passed (15)
   Start at  14:35:52
   Duration  6.23s
```

**15 of 15 passing, 0 failures.**

---

## Visual verification

Visual verification requires the dev server running locally. The implementation follows the recon spec exactly; scenario notes below describe what each path exercises.

**QuizStep — Regenerate All button with questions present:**
- "Regenerate All" button in the questions card header opens a dialog on click
- Dialog title: "Regenerate all questions?"
- Dialog description: "This will replace all current questions with newly AI-generated ones. Any edits you've made to questions will be lost."
- Cancel button (left) — closes dialog via `AlertDialogCancel`, no regeneration
- Regenerate button (right) — `AlertDialogAction` auto-closes dialog on click, then `handleGenerateQuiz` fires; step transitions into its loading/generating state

**QuizStep — empty-state "Generate Quiz" button:**
- Fires `handleGenerateQuiz` directly — NO dialog (no questions to lose)

**QuizStep — error-state "Retry generation" button:**
- Fires `generateMutation.reset()` + `handleGenerateQuiz` directly — NO dialog (questions were cleared before the failed mutation)

**Settings page — Back button disabled during save:**
- `isNavigating` is now destructured from `useStepNavigation` and passed to `WizardLayout`
- WizardLayout disables the Back button while `isNavigating` (i.e., `updateStep.isPending`) is true
- Matches the behaviour already present on Steps 2, 3, 5, 6, 7

---

## Files changed in scope

- `web/src/features/toolbox-talks/components/learning-wizard/steps/QuizStep.tsx`
  — Added `useState` to react import (line 3); added AlertDialog family imports from
  `@/components/ui/alert-dialog`; added `const [showRegenerateConfirm, setShowRegenerateConfirm] = useState(false)` after mutation declarations; changed "Regenerate All" button `onClick` from `handleGenerateQuiz` to `() => setShowRegenerateConfirm(true)`; added `AlertDialog` JSX above the "Save & Continue" button in the questions editor return branch

- `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/settings/page.tsx`
  — Added `isNavigating` to `useStepNavigation` destructure; added `isNavigating={isNavigating}` prop to `WizardLayout` after `onBack={goBack}`

---

## Files changed outside stated scope

None. `git diff --stat` shows exactly the two scoped implementation files plus `BACKLOG.md` (pre-existing scope correction).

---

## `AlertDialogAction` usage — confirmed correct

Used `AlertDialogAction` on the Regenerate confirm button. This is **correct** for this dialog, in contrast with Chunk 2's Step 2 Cancel dialog which used a plain `Button`.

Rationale:
- `AlertDialogAction` auto-closes the dialog on click (Radix Close primitive). That's exactly what's wanted here — the dialog closes, `handleGenerateQuiz` fires, and the step's own loading UI takes over.
- Chunk 2's Cancel dialog needed the dialog to **stay open** during the async delete mutation (to show "Discarding…" state and handle errors). A plain `Button` with `setShowCancelConfirm(false)` in the success branch gave that control.
- No conflict. Each dialog uses the primitive appropriate for its async behaviour.

---

## Non-destructive styling — confirmed

The Regenerate confirm button uses default `AlertDialogAction` styling — no `className` override, no `bg-destructive`. Matches ParseStep's Re-parse dialog (same rationale: not permanent data loss — user can regenerate again or hand-edit). The destructive red styling is reserved for the Cancel/Discard flow (Step 2) only.

---

## Empty-state and error-state buttons remain un-gated — confirmed

- `QuizStep.tsx` line ~294 (`Generate Quiz` in empty state): calls `handleGenerateQuiz` directly — unchanged
- `QuizStep.tsx` line ~263 (`Retry generation` in error state): calls `generateMutation.reset()` + `handleGenerateQuiz` directly — unchanged

Both correctly bypass the dialog; the dialog is only reachable via the "Regenerate All" button in the questions editor branch (only rendered when `hasQuestions` is true).

---

## ParseStep.tsx NOT modified — confirmed

`ParseStep.tsx` was read as a pattern reference only. The Re-parse dialog is fully implemented there (lines 314-330). No changes made to that file. `git diff --stat` confirms it is not in the diff.

---

## Settings page Back button now disables during step save — confirmed

`isNavigating` is `updateStep.isPending` (returned from `useStepNavigation` at line 80). Previously absent from the `settings/page.tsx` destructure, causing `WizardLayout` to receive `undefined` for `isNavigating` (treated as `false` — Back never disabled). Now passed correctly, matching all other step pages.

---

## Notable deviations from recon spec

None. Implementation follows the recon exactly:
- AlertDialog placed above "Save & Continue" button (at end of questions editor return, visible regardless of portal rendering)
- `onClick={() => setShowRegenerateConfirm(true)}` used inline (no named handler wrapper needed — no condition check required since the button is only rendered when `hasQuestions`)
- `AlertDialogAction onClick={handleGenerateQuiz}` — direct reference, no wrapper (recon §6 confirmed this is fine)

---

## Build output

```
npx tsc --noEmit  →  clean (no output)
npm run test      →  15 of 15 passing, 0 failures
```

Zero warnings introduced by this chunk.

---

## BACKLOG impact

**§26 Chunk 3 of 4 — complete. Ready for commit.**

Chunk 4 remains (reserved). §26 closes when Chunk 4 either ships content or is officially marked not-needed.
