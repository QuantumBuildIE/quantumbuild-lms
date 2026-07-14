# §26 Chunk 2 Recon — Cancel affordances on Steps 1 and 2

_Date: 2026-06-19_
_Branch: transval_
_Author: Claude Code (read-only — no code changed)_
_Trigger: §26 Chunk 1 shipped. This recon finalises implementation specifics for the frontend Cancel work._

---

## 1. Verification verdict — no drift

All five claims from the navigation recon hold against the current codebase.

| Claim | File:line | Holds? |
|---|---|---|
| Step 1 page footer renders "View drafts" only, no Cancel | [new/page.tsx:28-36](../../web/src/app/(authenticated)/admin/toolbox-talks/learnings/new/page.tsx#L28-L36) | ✅ |
| Step 2 page has empty navigation bar during parse-in-progress | [parse/page.tsx:41-43](../../web/src/app/(authenticated)/admin/toolbox-talks/learnings/%5BtalkId%5D/parse/page.tsx#L41-L43) `canGoBack=false, canGoNext=false`; WizardLayout renders no buttons | ✅ |
| Step 2 page has "Save & Continue" in idle/ready state, no Cancel | [ParseStep.tsx:332-350](../../web/src/features/toolbox-talks/components/learning-wizard/steps/ParseStep.tsx#L332-L350) | ✅ |
| `DeleteDraftDialog.tsx` exists and uses controlled `AlertDialog` | [DeleteDraftDialog.tsx:37-68](../../web/src/features/toolbox-talks/components/learning-wizard/components/DeleteDraftDialog.tsx#L37-L68) | ✅ |
| A mutation hook for `DELETE /api/toolbox-talks/{id}` exists and is in use | [use-toolbox-talks.ts:108-117](../../web/src/lib/api/toolbox-talks/use-toolbox-talks.ts#L108-L117); consumed in `ToolboxTalkDetail.tsx:61` and `ToolboxTalkList.tsx:84` | ✅ |

**One structural clarification:** `InputConfigStep` has **no props at all** — it is `export function InputConfigStep()`. There is no `onCancel` prop, no cancel callback of any kind. The component is fully self-contained; all navigation is handled in the page shell (`new/page.tsx`). This means Step 1 Cancel belongs in the page shell, not in the step component.

---

## 2. Step 1 Cancel specifics

### Button placement — recommendation: `leftFooter` slot in WizardLayout

`WizardLayout`'s navigation bar has two divs:

```jsx
// WizardLayout.tsx:64-92
<div className="mt-8 pt-4 border-t flex items-center justify-between gap-4">
  <div>                               // LEFT: Back button when canGoBack && onBack
    {canGoBack && onBack && <Button variant="ghost">Back</Button>}
  </div>
  <div className="flex items-center gap-3">   // RIGHT: footer slot + Continue
    {footer}
    {canGoNext && onNext && <Button>Continue</Button>}
  </div>
</div>
```

On Step 1: `canGoBack=false`, left div is empty. The `footer` slot renders "View drafts" on the right.

**Recommendation: add a `leftFooter?: React.ReactNode` prop to WizardLayout.** Render it in the left div. Cancel renders on the left; "View drafts" stays on the right. This matches the legacy's "Cancel bottom-left / Continue bottom-right" layout and correctly separates the destructive-navigation affordance from the persist-and-navigate affordance.

```
[Cancel]                   [View drafts]
```

This prop is also the natural extension point when Chunks 3+ add Cancel to Steps 3-7 (each step page passes its own `leftFooter` Cancel button without further WizardLayout changes).

**Alternative rejected:** Adding Cancel to the existing `footer` slot (right side) would cluster Cancel, View drafts, and Continue all on the right. Crowded and not conventional.

### "View drafts" coexistence — keep both

Cancel (left) and "View drafts" (right) are different actions with different semantics:
- Cancel → discard form state → navigate to talks list
- View drafts → preserve draft → navigate to drafts list

Both remain. They don't conflict.

### Confirmation dialog wording — Step 1

Step 1 has no DB row yet. Form state loss only — not a destructive DB operation.

| Element | Recommended text |
|---|---|
| Title | "Cancel creation?" |
| Description | "Any changes you've made won't be saved." |
| Confirm button | "Yes, cancel" — **no destructive styling** (no DB row deleted) |
| Cancel button | "Keep editing" |

The confirm button gets no `bg-destructive` override, matching the "informational" tone of `DeleteDraftDialog`'s cancel button vs its confirm button. This distinguishes Step 1 Cancel (reversible, no data lost server-side) from Step 2 Cancel (irreversible soft-delete).

### Navigation destination on confirm

`/admin/toolbox-talks/talks` — the talks list. Confirmed from BACKLOG §26 and matching legacy behaviour. Not the drafts list (that's "View drafts").

### Form state cleanup

No explicit cleanup needed. `InputConfigStep` uses `react-hook-form`; state lives in the component instance. On `router.push(...)`, the component unmounts and state is discarded automatically. No `useEffect` teardown or abort controller work required.

---

## 3. Step 2 Cancel specifics

### Button placement — same `leftFooter` slot, via parse/page.tsx

`parse/page.tsx` controls WizardLayout; it will pass the Cancel button via `leftFooter`. The button renders in the left div, visible in **both** parse states:

- **In-progress (spinner):** Left div gains Cancel. Right div remains empty (no Continue). Empty nav bar becomes: `[Cancel]`
- **Idle/ready (sections editor):** Left div has Cancel. Right div is empty from WizardLayout's perspective (the "Save & Continue" is rendered inline inside `ParseStep.tsx:332-350`, not via WizardLayout's Continue slot). So: `[Cancel]   [Save & Continue is inside card]`

This correctly fills the gap identified as row 10 in the navigation recon ("functional degradation — step 2 has no buttons at all during parse").

### Confirmation dialog wording — Step 2

Step 2 deletes a DB row — genuinely destructive.

| Element | Recommended text |
|---|---|
| Title | "Discard this learning?" |
| Description | "The draft will be permanently deleted and cannot be recovered." |
| Confirm button | "Yes, discard" — **with destructive styling** matching `DeleteDraftDialog`: `className="bg-destructive text-destructive-foreground hover:bg-destructive/90"` |
| Cancel button | "Keep editing" |

### In-progress vs idle — same button

The Cancel button is identical in both parse states. The dialog wording is the same. No state-dependent branching needed in the Cancel affordance itself.

### Mutation behaviour

1. **Disable confirm button + show `Deleting…` label** while `deleteMutation.isPending`.
2. **Wait for success then navigate** — `mutateAsync(talkId)` resolves → `router.push('/admin/toolbox-talks/talks')`. Do not navigate optimistically; the soft-delete is fast (~200ms) and navigating before confirmation could leave the user looking at a deleted resource.
3. **Error handling on failure** — `toast.error(...)` matching the `ToolboxTalkDetail` consumer pattern (line 77-78). Dialog stays open (user can retry or dismiss).

Example (from existing consumer, `ToolboxTalkDetail.tsx:69-79`):

```tsx
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

Chunk 2's implementation follows the same shape. Success toast: "Draft discarded." Error toast: the mutation error message.

### Scope: Step 2 only for this chunk

The BACKLOG §26 notes "Step 2 (and arguably any draft state from Step 2 onward)." For this chunk: Step 2 only. The `leftFooter` extension to WizardLayout naturally supports adding Cancel to Steps 3-7 in a future chunk by each step page passing its own `leftFooter` Cancel button.

---

## 4. Mutation hook reuse

**Hook: `useDeleteToolboxTalk`**

Location: [use-toolbox-talks.ts:108-117](../../web/src/lib/api/toolbox-talks/use-toolbox-talks.ts#L108-L117)

```ts
export function useDeleteToolboxTalk() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => deleteToolboxTalk(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TOOLBOX_TALKS_KEY });
    },
  });
}
```

Signature: `() => UseMutationResult<void, Error, string>`. Call `mutateAsync(talkId)`.

**Existing consumers and their patterns:**

| Consumer | Usage |
|---|---|
| `ToolboxTalkDetail.tsx:61` | `const deleteMutation = useDeleteToolboxTalk()` → `await deleteMutation.mutateAsync(talk.id)` in try/catch → `router.push(basePath)` on success |
| `ToolboxTalkList.tsx:84` | Same hook, different post-delete action (list refresh handled by query invalidation) |

`parse/page.tsx` will add the hook identically to `ToolboxTalkDetail`. The `talkId` is already in scope from `useParams()` (line 15 of parse/page.tsx).

Hook exists, is stable, and has an established consumer pattern. No changes to the hook itself.

---

## 5. StepIndicator interaction note

With Cancel visible in the nav bar, users have two explicit paths away from Step 2:

| Path | Draft fate | Backend effect |
|---|---|---|
| **Cancel (Chunk 2)** | Deleted (soft-delete) | Parse job exits cleanly (Chunk 1 guard) |
| **StepIndicator → Step 1** | Preserved (draft continues) | Parse job continues running |

These serve different intents: Cancel = "abandon this creation attempt entirely"; StepIndicator click = "go back to review/change input, keep the draft". Both are valid. The distinction should be clear to users because Cancel is labelled "Cancel" and navigates to the talks list, while StepIndicator is labelled "1 Input & Config" and takes users to Step 1 of the current draft.

**Implementation prompt note (not a code change):** The impl prompt should note that StepIndicator click from Step 2 during parse is intentionally not modified by this chunk. If in-progress parse plus StepIndicator navigation ever becomes a user confusion point, a confirmation on StepIndicator mid-parse would be Chunk 3+ territory.

---

## 6. Sized implementation chunk

### Files affected

**`WizardLayout.tsx`** — 1 file
- Add `leftFooter?: React.ReactNode` to `WizardLayoutProps` interface
- Destructure it in the component
- Render `{leftFooter}` inside the left `<div>` (after the existing Back button conditional)
- Net: +4-5 lines

**`new/page.tsx`** — Step 1 cancel
- Import: `AlertDialog`, `AlertDialogAction`, `AlertDialogCancel`, `AlertDialogContent`, `AlertDialogDescription`, `AlertDialogFooter`, `AlertDialogHeader`, `AlertDialogTitle` from `@/components/ui/alert-dialog`
- State: `const [showCancelConfirm, setShowCancelConfirm] = useState(false)` (+ `useState` import already present via `useValidationRuns`... actually `useState` is not currently imported in new/page.tsx — needs to be added)
- Cancel button JSX: `<Button variant="ghost" onClick={() => setShowCancelConfirm(true)}>Cancel</Button>`
- AlertDialog JSX: controlled `open={showCancelConfirm}` / `onOpenChange={setShowCancelConfirm}` with Step 1 wording
- Wire `leftFooter={<cancelButton/>}` into `WizardLayout`
- Net: ~30-35 lines added

**`parse/page.tsx`** — Step 2 cancel
- Import: `AlertDialog*` components (same as above), `useDeleteToolboxTalk` from `@/lib/api/toolbox-talks`, `useState` (already imported via hooks? — confirm: no, `useState` is not currently in parse/page.tsx imports)
- State: `const [showCancelConfirm, setShowCancelConfirm] = useState(false)`
- Mutation: `const deleteMutation = useDeleteToolboxTalk()`
- Cancel handler: async try/catch → `deleteMutation.mutateAsync(talkId)` → `router.push('/admin/toolbox-talks/talks')`
- Cancel button JSX: `<Button variant="ghost" onClick={() => setShowCancelConfirm(true)}>Cancel</Button>`
- AlertDialog JSX: controlled dialog with Step 2 wording + destructive confirm button + `isLoading={deleteMutation.isPending}` disable
- Wire `leftFooter={<cancelButton/>}` into `WizardLayout`
- Net: ~45-50 lines added

### Summary

| File | Change | Net lines |
|---|---|---|
| `WizardLayout.tsx` | Add `leftFooter` prop + render | +5 |
| `new/page.tsx` | Step 1 Cancel button + dialog + leftFooter wire | +33 |
| `[talkId]/parse/page.tsx` | Step 2 Cancel button + dialog + leftFooter wire + delete mutation | +48 |

**Total estimated diff: ~86 lines added, 0 deleted.**
**Effort estimate: 2-3 hours** (one clear implementation path, no ambiguity).

No new component file needed. Dialogs inline in page.tsx files (matching the existing page-level dialog pattern in `ToolboxTalkDetail.tsx`). If Steps 3-7 get Cancel in a future chunk, extract a `CancelWizardDialog` component then.

---

## 7. Files read

| File | Purpose |
|---|---|
| `BACKLOG.md` §26 | Confirmed Chunk 2 scope wording |
| `docs/25/post-close-navigation-recon.md` | Navigation recon — claims verified |
| `docs/parse-handler-cancellation-investigation.md` | Backend safety story |
| `docs/26/chunk-1-fix.md` | Confirmed Chunk 1 landed — 696 tests passing |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/InputConfigStep.tsx` | Step 1 component: no `onCancel` prop, Continue is `type="submit"` at bottom |
| `web/src/app/(authenticated)/admin/toolbox-talks/learnings/new/page.tsx` | Step 1 page shell: `footer="View drafts"`, no Cancel |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/ParseStep.tsx` | Step 2: prop interface `{talkId, onContinue}` only, AlertDialog pattern confirmed |
| `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/parse/page.tsx` | Step 2 page shell: `canGoBack=false`, `canGoNext=false`, no `footer` |
| `web/src/features/toolbox-talks/components/learning-wizard/components/WizardLayout.tsx` | Layout: left div = Back, right div = `{footer}` + Continue |
| `web/src/features/toolbox-talks/components/learning-wizard/components/DeleteDraftDialog.tsx` | AlertDialog convention: controlled state, destructive confirm styling |
| `web/src/lib/api/toolbox-talks/use-toolbox-talks.ts:108-117` | `useDeleteToolboxTalk` hook: signature, invalidation, pattern |
| `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx:61,69-79` | Existing `useDeleteToolboxTalk` consumer pattern |
| `web/src/features/toolbox-talks/components/learning-wizard/components/StepIndicator.tsx` | Confirmed: navigates, does not abort |

---

## 8. Report written

`docs/26/chunk-2-recon.md` — this file.

---

## 9. Out of scope items flagged

None — no drift discovered that requires stopping.

The following were confirmed in-scope and settled:
- `leftFooter` is a new prop; WizardLayout does not yet have it (confirmed: only `footer` exists)
- Neither `InputConfigStep` nor `ParseStep` have existing `onCancel` props (confirmed absent)
- `useDeleteToolboxTalk` exists and is consumed cleanly (confirmed)

The following remain out of scope per §26 BACKLOG:
- Modifying StepIndicator click behaviour during in-progress parse
- Adding Cancel to Steps 3-7 (extensible via `leftFooter` but not this chunk)
- Aborting in-flight AI API calls server-side
- Reaper for orphaned drafts from browser-close / network-drop
- Backend changes (Chunk 1 closed the backend story)
- Chunk 3 confirmation dialogs for Re-parse / Regenerate All
- `isNavigating` prop fix for settings/page.tsx (Chunk 3)
