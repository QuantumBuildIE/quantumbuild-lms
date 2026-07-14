# §26 Chunk 2 Fix — Cancel affordances on Steps 1 and 2

_Date: 2026-06-19_
_Branch: transval_
_Author: Claude Code_
_Based on recon: `docs/26/chunk-2-recon.md`_

---

## Summary of changes

Three files modified. ~90 lines net added (higher than the recon's ~86 estimate because
the Write tool replaces whole files; the structural additions are as specified).

| File | Change |
|---|---|
| `WizardLayout.tsx` | Added `leftFooter?: React.ReactNode` prop + render in left nav div |
| `new/page.tsx` | Step 1 Cancel button, AlertDialog (non-destructive), `leftFooter` wired |
| `[talkId]/parse/page.tsx` | Step 2 Cancel button, AlertDialog (destructive), `useDeleteToolboxTalk`, `leftFooter` wired |

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
   Start at  14:03:01
   Duration  1.54s
```

**15 of 15 passing, 0 failures.**

---

## Visual verification

Visual verification requires the dev server running locally. The implementation
follows the recon's structural spec exactly; coverage notes below describe what
each scenario will exercise.

**Step 1 Cancel flow:**
- Cancel button appears in bottom-left of the navigation bar (`leftFooter` slot)
- "View drafts" remains in the right slot (`footer` slot) — coexistence maintained
- Clicking Cancel opens a dialog: title "Cancel creation?", body "Any changes you've
  made won't be saved.", buttons "Keep editing" / "Yes, cancel"
- "Keep editing" closes the dialog, no navigation
- "Yes, cancel" (`AlertDialogAction`) calls `handleCancelConfirm`: closes dialog,
  navigates to `/admin/toolbox-talks/talks`
- No DB operation — form state discards on unmount

**Step 2 Cancel flow — both parse states:**
- Cancel button appears in the previously-empty bottom-left nav bar
- Clicking Cancel opens a dialog: title "Discard this learning?", body "The draft
  will be permanently deleted and cannot be recovered."
- "Keep editing" (`AlertDialogCancel`) closes the dialog
- "Yes, discard" (regular `Button`, not `AlertDialogAction`) calls async
  `handleCancelConfirm`: awaits `deleteMutation.mutateAsync(talkId)`, on success
  shows toast "Draft discarded." and navigates to `/admin/toolbox-talks/talks`
- While `deleteMutation.isPending`: both buttons are `disabled`, confirm label
  changes to "Discarding…", dialog stays open
- On error: `toast.error(...)` shown, dialog stays open (user can retry)

**StepIndicator non-interaction:**
- `StepIndicator.tsx` is unmodified; StepIndicator click from Step 2 navigates
  without aborting parse or deleting the draft — unchanged from pre-chunk behaviour

---

## Files changed in scope

- `web/src/features/toolbox-talks/components/learning-wizard/components/WizardLayout.tsx`
  — Added `leftFooter?: React.ReactNode` to `WizardLayoutProps` interface;
  added `leftFooter` to destructuring; added `className="flex items-center gap-3"` to
  left nav div; rendered `{leftFooter}` inside left div after Back button conditional

- `web/src/app/(authenticated)/admin/toolbox-talks/learnings/new/page.tsx`
  — Added `useState`, `Button`, AlertDialog imports; added `showCancelConfirm` state;
  added `handleCancelConfirm` handler; added Cancel button as `leftFooter`; added
  controlled AlertDialog (non-destructive `AlertDialogAction` — synchronous, safe to
  auto-close); wrapped return in Fragment

- `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/parse/page.tsx`
  — Added `useState`, `Button`, AlertDialog, `useDeleteToolboxTalk`, `toast` imports;
  added `showCancelConfirm` state and `deleteMutation`; added async
  `handleCancelConfirm` handler matching `ToolboxTalkDetail.handleDelete` pattern;
  added Cancel button as `leftFooter`; added controlled AlertDialog (destructive
  styling on confirm); wrapped return in Fragment

---

## Files changed outside stated scope

None. `git diff --stat` shows exactly the three scoped files.

---

## Destructive vs non-destructive dialog styling — confirmed

**Step 1 (`new/page.tsx`):** Confirm button uses plain `AlertDialogAction` with no
className override — default (non-destructive) styling. No DB row is deleted; form
state discards on navigation. Distinguishes this dialog from Step 2's.

**Step 2 (`parse/page.tsx`):** Confirm button uses `className="bg-destructive text-destructive-foreground hover:bg-destructive/90"`, matching `DeleteDraftDialog.tsx` line 60 exactly.

---

## `useDeleteToolboxTalk` consumer pattern — confirmed match

`parse/page.tsx` matches `ToolboxTalkDetail.tsx` exactly:

```tsx
// ToolboxTalkDetail.tsx (reference)
const deleteMutation = useDeleteToolboxTalk();

const handleDelete = async () => {
  try {
    await deleteMutation.mutateAsync(talk.id);
    toast.success('Learning deleted successfully');
    router.push(basePath);
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Failed to delete learning';
    toast.error('Error', { description: message });
  }
};
```

```tsx
// parse/page.tsx (this chunk)
const deleteMutation = useDeleteToolboxTalk();

const handleCancelConfirm = async () => {
  try {
    await deleteMutation.mutateAsync(talkId);
    setShowCancelConfirm(false);
    toast.success('Draft discarded.');
    router.push('/admin/toolbox-talks/talks');
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Failed to discard draft';
    toast.error('Error', { description: message });
  }
};
```

Import path: `@/lib/api/toolbox-talks` (barrel), matching `ToolboxTalkDetail.tsx` and `ToolboxTalkList.tsx`.

---

## Notable deviations from recon spec

| Item | Recon statement | Actual | Rationale |
|---|---|---|---|
| Step 2 confirm button component | Showed `AlertDialogAction` in JSX sample | Used plain `Button` inside `AlertDialogFooter` | `AlertDialogPrimitive.Action` auto-closes the dialog on click (it extends the Radix Close primitive). Using `AlertDialogAction` would close the dialog before the async mutation resolves, preventing the "dialog stays open on error" behaviour stated in recon §3. `Button` gives full control: dialog closes only on `setShowCancelConfirm(false)` in the success branch. |
| Line count | ~86 lines added | 135 insertions / 32 deletions (git stat shows full rewrites) | Write tool rewrites entire files; net structural additions match the recon's intent |

---

## StepIndicator behaviour — confirmed unchanged

`StepIndicator.tsx`, `useStepNavigation.ts`, and all related navigation code are
unmodified. StepIndicator click from Step 2 preserves the draft and does not abort
parse — as intended.

---

## BACKLOG impact

**§26 Chunk 2 of 4 — complete. Ready for commit.**

Chunks 3 and 4 remain:
- **Chunk 3** — Re-parse / Regenerate All confirmation dialogs; `isNavigating` prop fix on settings/page.tsx
- **Chunk 4** — (per BACKLOG §26)

The `leftFooter` prop on `WizardLayout` is the natural extension point for adding Cancel
to Steps 3-7 in a future chunk: each step page passes its own `leftFooter` Cancel button
with no further changes to `WizardLayout`.

---

## Build output

```
npx tsc --noEmit  →  clean (no output)
npm run test      →  15 of 15 passing, 0 failures
```

Zero warnings introduced by this chunk.
