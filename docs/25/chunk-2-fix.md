# §25 Chunk 2 — ParseStep + QuizStep Visual Polish: Implementation Report

_Date: 2026-06-19_  
_Branch: transval_  
_Author: Claude Code_

---

## Summary of changes

Three files modified, all in scope:

| File | Change |
|------|--------|
| `web/src/features/toolbox-talks/components/learning-wizard/steps/ParseStep.tsx` | Card wrap + Re-parse button + confirmation dialog + empty state upgrade |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/QuizStep.tsx` | Card-lite wrapper + WizardSectionDivider + empty state upgrade |
| `web/src/features/toolbox-talks/components/learning-wizard/components/QuizSettingsPanel.tsx` | h3 removal (title ownership transferred to WizardSectionDivider) |

---

## Test results

**`npx tsc --noEmit`:** Passed — exit code 0, no output (zero errors)

**`npm run test`:**
```
Test Files  3 passed (3)
Tests       15 passed (15)
```

No new failures introduced. Pre-existing test count unchanged.

---

## Visual verification

Dev server confirmed running at `http://localhost:3000`. Verified the following surfaces:

**Step 2 (ParseStep) — ready state (sections present):**
- shadcn `Card` wraps the section list. `CardHeader` renders "Sections" title and section count description. `CardAction` positions the "Re-parse" button top-right of the header, matching the layout the `CardAction` slot is designed for (grid column 2, row span 2).
- Re-parse button shows `RefreshCw` icon + "Re-parse" label, `variant="outline" size="sm"`, disabled while parsing or saving.
- The `sectionsError` paragraph is inside `CardContent` below `SectionList`, with `mt-4` spacing.
- Save & Continue button sits outside the Card in the outer `space-y-4` wrapper.

**Re-parse confirmation dialog flow:**
- Clicking "Re-parse" with sections present opens `AlertDialog`.
- Dialog shows title "Re-parse content?" and description explaining section edits will be lost.
- Clicking "Cancel" closes dialog, no parse fired.
- Clicking "Re-parse" (confirm) closes dialog and calls `handleParse()` — sections cleared, parse mutation fires.
- Clicking "Re-parse" on the empty-state button (no sections) calls `handleParse()` directly, no dialog (nothing to discard).

**Step 2 (ParseStep) — empty state:**
- Outer container is `py-20` (was `py-16`).
- Icon circle is `rounded-full bg-primary/10 p-4 text-primary` — filled with primary/10 tint, slightly larger padding, icon in primary colour.

**Step 3 (QuizStep) — ready state (questions present):**
- Card-lite wrapper: `rounded-xl border shadow-sm bg-card overflow-hidden`. Header bar `px-6 py-4 border-b` contains "Quiz Questions" title + question count description left, "Regenerate All" button right.
- `SectionQuestionGroup` renders flush below the header bar inside the wrapper — no double border, no duplicate heading, `overflow-hidden` on the outer div makes SectionQuestionGroup's own `border rounded-lg` tuck cleanly inside the rounded corners.
- `questionsError` paragraph renders outside the wrapper in the outer `space-y-6`.
- `WizardSectionDivider number="2a" label="Quiz Settings"` renders between the questions wrapper and `QuizSettingsPanel` — monospace "2a", uppercase "QUIZ SETTINGS", horizontal rule to edge. Uses `text-foreground` / `border-border` tokens (Chunk 1 patch confirmed in place).
- `QuizSettingsPanel` renders with no "Quiz Settings" h3 — the divider owns the title; the panel content begins directly with "Passing Score (%)".

**Step 3 (QuizStep) — empty state:**
- Same upgrade as ParseStep: `py-20`, `bg-primary/10 p-4 text-primary` icon circle.

---

## QuizSettingsPanel caller grep result and decision

Grep for `QuizSettingsPanel` across `web/src/**/*.tsx` found two distinct components with the same name in different directories:

| File | Import path | Caller |
|------|-------------|--------|
| `learning-wizard/components/QuizSettingsPanel.tsx` | `../components/QuizSettingsPanel` | `learning-wizard/steps/QuizStep.tsx` only |
| `create-wizard/steps/quiz/QuizSettingsPanel.tsx` | `./quiz/QuizSettingsPanel` | `create-wizard/steps/QuizStep.tsx` only |

The two are separate files with separate prop interfaces — no shared component. The `learning-wizard` version has exactly one caller. **Decision: remove the h3 entirely** (cleaner, no prop added). The legacy `create-wizard` version is untouched.

---

## Card vs Card-lite asymmetry — intentional

**ParseStep uses shadcn `<Card>`** — `SectionList` has no conflicting outer container. The Card primitives (`CardHeader`, `CardTitle`, `CardDescription`, `CardAction`, `CardContent`) compose cleanly; no double border, no duplicate heading.

**QuizStep uses a hand-rolled "Card-lite" div** — `SectionQuestionGroup` has its own `<div className="border rounded-lg overflow-hidden">` outer container at line 28 and its own collapsible "Quiz Questions (N)" internal header button. Wrapping in shadcn `<Card>` would produce a double border (`rounded-xl border` wrapping `rounded-lg border`) and a duplicate "Quiz Questions" heading. The Card-lite approach (`rounded-xl border shadow-sm bg-card overflow-hidden`) achieves the same visual weight without the component's structural primitives. No changes to `SectionQuestionGroup.tsx`.

This asymmetry is documented here and in the recon (`docs/25/chunk-2-recon.md §2.5`).

---

## AlertDialog convention match

The closest existing usage in the same feature directory is `learning-wizard/components/DeleteDraftDialog.tsx`, which uses:
- `AlertDialog open={...} onOpenChange={...}` — controlled state (no `AlertDialogTrigger`)
- `AlertDialogAction` and `AlertDialogCancel` primitives (not plain `Button`)
- `AlertDialogAction` with `className` override for destructive styling

The Re-parse dialog matches this pattern: uses `AlertDialogAction` / `AlertDialogCancel` primitives, controlled state via `showReparseConfirm` / `setShowReparseConfirm`. The confirm action uses default `AlertDialogAction` styling (not destructive red) — re-parsing is recoverable data loss (unsaved edits, not permanent deletion), so destructive styling would be misleading.

---

## Notable deviations from recon spec

1. **`sections.length > 0` check for dialog gate**: The prompt spec used `sections.length > 0`; implementation uses `form.getValues('sections').length > 0` in `handleReparseClick`. These are functionally equivalent — `sections` in the component body is `form.watch('sections')` (reactive), and `form.getValues` inside a callback reads the same value. Both work correctly; the callback version is slightly safer since it reads at call-time rather than capturing a closure over the watched value.

2. **AlertDialog positioned as sibling outside Card**: The AlertDialog is a sibling of the Card in the outer `space-y-4` div. It manages its own portal so the DOM position is irrelevant to rendering; this keeps the JSX logically grouped with the action that triggers it (close to the Re-parse button's Card, above Save & Continue).

3. **`questionsError` outside Card-lite wrapper**: The prompt spec confirmed this placement — the error paragraph stays in the outer `space-y-6` after the wrapper. Implemented as specified.

---

## BACKLOG impact

- **§25 status:** Open — Chunk 2 of 5 complete. Chunks 3, 4, and 5 remain.
- **§5.7 status:** Still blocked — unblocks at Chunks 1–4 all complete.
- **No new BACKLOG entries** — the Re-parse confirmation dialog concern from the Chunk 2 recon §4 is resolved within this chunk.

---

## Files changed in scope

- `web/src/features/toolbox-talks/components/learning-wizard/steps/ParseStep.tsx`
- `web/src/features/toolbox-talks/components/learning-wizard/steps/QuizStep.tsx`
- `web/src/features/toolbox-talks/components/learning-wizard/components/QuizSettingsPanel.tsx`

## Files changed outside stated scope

None.

## Build output

`npx tsc --noEmit`: pass (exit 0, no warnings)  
`npm run test`: 3/3 test files, 15/15 tests, no new failures
