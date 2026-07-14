# Recon: Dead-code hooks + edit-route toggle behaviour

> **Correction (2026-07-16):** The "aside" in Part 2 describing a
> backslash-corrupted route literal at `CourseList.tsx:262` is
> factually wrong. The code in that file has forward slashes and has
> never contained backslashes at that location — verified by direct
> file inspection and git history back to the initial extraction
> commit. This finding was not acted on. The main findings in this
> document (dead hooks confirmed, DTO still live, edit-route toggle
> not needed) were independently verified during chunk 3
> implementation and hold up.

**Date:** 2026-07-09
**Scope:** Read-only investigation for chunk 3 of `docs/course-in-new-wizard-scoping-v2.md`. No code changes made.

---

## Headline

**Dead hooks confirmed, edit toggle not needed — small cleanup chunk.**

`useAddCourseItem`/`useRemoveCourseItem` are genuinely unused frontend code, not an unfinished refactor — `CourseForm` already has full add/remove/reorder capability for course items in both create *and* edit mode, built entirely on local React state plus the bulk `PUT /items` endpoint (`useUpdateCourseItems`). The two singular-item hooks solve a per-item-network-call problem that the batch-first UX pattern never needed. The edit route was never gated by `UseNewCourseCreation` for a structural reason, not an oversight: there is only one course edit UI (`CourseForm`) regardless of which path created the course, so there is nothing for a toggle to switch between.

---

## Part 1 — The dead-code hooks

### What they do

`web/src/lib/api/toolbox-talks/use-courses.ts:85-107`:

```ts
export function useAddCourseItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, data }) => addCourseItem(courseId, data),
    onSuccess: (_, { courseId }) => {
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALK_COURSES_KEY, courseId] });
    },
  });
}

export function useRemoveCourseItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, talkId }) => removeCourseItem(courseId, talkId),
    onSuccess: (_, { courseId }) => {
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALK_COURSES_KEY, courseId] });
    },
  });
}
```

- **Endpoints called:** `POST /toolbox-talks/courses/{courseId}/items` (add) and `DELETE /toolbox-talks/courses/{courseId}/items/{talkId}` (remove) — thin wrappers around `addCourseItem`/`removeCourseItem` in `courses.ts:155-174`.
- **Cache invalidation:** only the single course-detail key `[...TOOLBOX_TALK_COURSES_KEY, courseId]`. Does not invalidate the course *list* key. Not an issue in practice (they're never called), but worth noting if they were ever wired in: a course-list view showing `talkCount` would go stale after an add/remove unless the list key were also invalidated. `useUpdateCourseItems` (the hook that *is* used) has the identical narrow invalidation, so this isn't a hooks-only defect — it matches the codebase's established (if slightly incomplete) pattern for this file.
- **Error handling:** none beyond the default `useMutation` rejected-promise behaviour — no `onError`. This matches every other mutation hook in the file (`useCreateToolboxTalkCourse`, `useUpdateToolboxTalkCourse`, `useDeleteToolboxTalkCourse` are equally bare); error handling in this codebase's convention lives in the calling component's `try/catch` around `mutateAsync`, not in the hook.
- **Maturity:** production-ready in style — no stub markers, no TODOs, structurally identical to the other (used) hooks in the same file. Nothing about the code itself signals "prototype."

### Reference search

Grepped `web/src` for `useAddCourseItem`, `useRemoveCourseItem`, and the underlying `addCourseItem`/`removeCourseItem` API functions. Confirmed: **zero references anywhere outside their own definitions** in `use-courses.ts` and `courses.ts`. No typo'd variants, no aliased imports, no test files, no comments mentioning them. The "unreferenced" finding from the compose-existing recon holds exactly as stated.

### Git history

```
git log --follow --oneline -- web/src/lib/api/toolbox-talks/use-courses.ts
9e575bf feat: QuantumBuild LMS - standalone LMS extracted from Rascor (Core + ToolboxTalks)

git log --oneline -- web/src/lib/api/toolbox-talks/courses.ts
b46e9f4 feat: Add Learning code field (e.g., MHS-001) with auto-generation
9e575bf feat: QuantumBuild LMS - standalone LMS extracted from Rascor (Core + ToolboxTalks)
```

Both files were introduced at `9e575bf`, the squashed extraction commit that split this LMS out of a larger Rascor monorepo — pre-extraction history isn't available in this repo, so there's no commit-by-commit trail showing whether the hooks were ever called before extraction. `courses.ts`'s only other touch (`b46e9f4`) added the unrelated Learning-code field. **Practical conclusion:** these hooks have been dead for the entire life of this repository (546 commits) and were never modified after their introduction — consistent with "dead since day one," not "recently orphaned by a refactor."

### Backend endpoints

All three item-mutation endpoints live on `ToolboxTalkCoursesController` with the same `Learnings.Manage` policy:

| Endpoint | Handler | Used by frontend? |
|---|---|---|
| `POST /{id}/items` | `AddCourseItemCommandHandler` | No |
| `DELETE /{id}/items/{talkId}` | `RemoveCourseItemCommandHandler` | No |
| `PUT /{id}/items` | `UpdateCourseItemsCommandHandler` | **Yes** — `useUpdateCourseItems`, called by `CourseForm` |

Reading all three handlers:

- `AddCourseItemCommandHandler` — validates the course exists, the talk exists, **the talk's `Status == Published`**, and the talk isn't already in the course. Inserts one `ToolboxTalkCourseItem`.
- `RemoveCourseItemCommandHandler` — validates the course and item exist, soft-deletes the one item. No extra guard.
- `UpdateCourseItemsCommandHandler` (bulk, the one actually used) — validates no duplicate talk IDs in the request, all talks exist, **all talks are `Published`** (identical guard to `AddCourseItemCommandHandler`), then diffs the submitted list against existing DB rows: soft-deletes anything missing from the new list, upserts (`OrderIndex`/`IsRequired`) everything present, inserts anything new.

**The bulk handler is a strict superset of add + remove.** It applies the same Published-only guard as `AddCourseItemCommandHandler` and produces the same soft-delete effect as `RemoveCourseItemCommandHandler`, batched over the whole item list in one transaction. There is no divergent validation or workflow-state gating on the singular endpoints that the bulk endpoint lacks — nothing about them suggests a different intended use case (e.g. "add items to an already-published course without touching the rest").

### Test coverage

Grepped `tests/` for `AddCourseItem`, `RemoveCourseItem`, `UpdateCourseItems`, and `/items`. Only one relevant file exists, `tests/QuantumBuild.Tests.Integration/ToolboxTalks/CourseCompositionTests.cs`, and it exercises **only** `PUT /api/toolbox-talks/courses/{id}/items` (the bulk endpoint) — two tests, both about the Published-talk guard (`UpdateCourseItems_AddingDraftTalk_ReturnsBadRequest`, `UpdateCourseItems_WithOnlyPublishedTalks_ReturnsOk`). **Zero test coverage exists for `POST /items` or `DELETE /items/{talkId}`.** This mirrors the frontend picture exactly: the codebase's test author, like its frontend author, only cared about the bulk path.

---

## Part 2 — The edit-route context

### `courses/[id]/edit/page.tsx`

Loads the course via `useToolboxTalkCourse(id)`, then renders a two-tab layout:
- **Details** — `<CourseForm course={course} />`, unconditional, no toggle check (`useCoursePreference` is not imported here at all).
- **Validation** — `ValidationHistoryTab` for the course's translation-validation run history.

`CourseForm` receives the full `ToolboxTalkCourseDto` (title, description, all settings flags, `items[]` with denormalised talk display fields, `translations[]`) as `initialCourse`-equivalent via the `course` prop.

### `CourseForm` in edit mode vs create mode

They are **the same component**, branching only on `isEditing = !!course`:

| Aspect | Create mode | Edit mode |
|---|---|---|
| Metadata fields (title, description, isActive, sequential completion, certificate, refresher, auto-assign) | Editable | Editable — identical fields, identical form |
| Course items (add/remove/reorder/toggle-required) | Full local-state CRUD via `AddTalksDialog` + dnd-kit drag reorder + per-row remove button | **Identical** — same `courseItems` local state, same dialog, same drag reorder, same remove button |
| Item persistence | One `createToolboxTalkCourse` call with `items[]` inline | One `updateToolboxTalkCourse` call (metadata) **followed by** one `useUpdateCourseItems` call (bulk item sync) |
| Assignments section (`CourseAssignmentsList` + `AssignCourseDialog`) | Not rendered (no course ID yet) | Rendered — lets an admin assign the existing course to employees |

Item add/remove/reorder is **not partially implemented in edit mode** — it's the full same feature as create mode, because `CourseForm` was designed as one unified authoring surface from the start. On save, edit mode fires `updateItemsMutation.mutateAsync({ courseId, data: { items: courseItems.map(...) } })` — i.e. **`useUpdateCourseItems`, the bulk hook, exactly as in create mode.** `useAddCourseItem`/`useRemoveCourseItem` are never reached from either mode.

### Legacy course edit experience

There is no separate legacy course edit UI to characterise. `ToolboxTalkCourse`/`ToolboxTalkCourseItem` are a single entity model regardless of which authoring path produced them — both the new compose-existing path (`CourseForm` → `CreateToolboxTalkCourseCommandHandler`) and the legacy split-and-author wizard (`ContentCreationSessionService`'s course-publish step) write into the same tables via (functionally) the same creation command. `courses/[id]/edit/page.tsx` is the only edit route in the app (confirmed by grep — no other `*course*edit*` or `*edit*course*` page exists), and it always renders `CourseForm`, irrespective of how the course being edited was originally created. So the scoping doc's framing — "edit stays reachable regardless of toggle because `CourseForm` is the existing entry point compose-existing was designed for" — is correct in outcome, but understates the reason slightly: it's not that edit deliberately ignores the toggle, it's that **the toggle was only ever wired at the two course-creation entry points** (`courses/new/page.tsx` and `CourseList.tsx`'s "Create New" button) and never had any reason to reach the edit route, because there has only ever been one edit UI.

### Aside — unrelated bug spotted in passing

Not part of the two questions in scope, but worth flagging since it sits directly next to this investigation: `CourseList.tsx:260-264`'s "Create New" button *does* already gate on `useCoursePreference` (contradicting the earlier `docs/course-in-new-wizard-scoping-v2.md` recon's claim that this was still an unwired gap — chunk 1 evidently wired it), but the new-path string is written with **backslashes**: `'\admin\toolbox-talks\courses\new'` instead of `/admin/toolbox-talks/courses/new`. In a JS string literal, `\t` inside `\toolbox-talks` and `\n` inside `\new` are real escape sequences (tab, newline) — so this is not merely a cosmetic slash mix-up, the resulting string is corrupted and `router.push()` will not navigate to the new-course route when the toggle is on. Flagging for awareness; not fixed here per the read-only recon scope.

---

## Part 3 — Situation categorisation

**(a) Dead hooks + edit UX doesn't need them — simple cleanup.**

`CourseForm`'s edit mode already has complete, working add/remove/reorder functionality for course items, built on local component state plus the bulk `PUT /items` endpoint. The singular add/remove hooks aren't an unfinished piece of that feature — they're leftover surface from a per-item-call design that the actual UX (batch everything client-side, sync once on Save) never adopted. There's no post-publish-only use case they uniquely serve either: the bulk endpoint applies the identical Published-talk guard and produces the identical soft-delete effect, and it's exercisable at any time the course exists (not gated to draft-course-only or any particular course state).

(b), (c), (d) do not apply — no caller was missed (Part 1 reference search was exhaustive and cross-checked against git history and test coverage), and there's no partial/unfinished edit UX to speak of (Part 2 found edit mode fully-featured, identical to create mode for item management).

---

## Part 4 — Edit-route toggle question

**Recommendation: don't gate. This is not contingent on the hooks decision — it falls out of Part 2's structural finding regardless.**

The "arguments against gating" bullets in the task brief are all confirmed true by this recon, not merely plausible:
- `CourseForm` is confirmed to be the only edit UI in the app — there is no legacy edit UI for a toggle to switch away from. Gating the edit route would only ever have one branch to render, making the toggle a no-op by construction.
- Existing courses (regardless of creation path) share one entity model and are all editable through the same form today — there's no "legacy-shaped data that breaks the new form" risk, because there's no second form with different assumptions about the data shape.
- The scoping doc's "void legacy courses at cutover" plan (§ Existing course data) means that once `UseNewCourseCreation` flips to `true` in Production, this becomes moot anyway — pre-cutover legacy courses won't exist to edit.

No code change is needed for the edit route as part of chunk 3.

---

## Part 5 — Recommended chunk 3 shape

**Small cleanup chunk**, per category (a):

1. **Delete the dead frontend surface:**
   - `useAddCourseItem` and `useRemoveCourseItem` in `web/src/lib/api/toolbox-talks/use-courses.ts:85-107`.
   - `addCourseItem` and `removeCourseItem` in `web/src/lib/api/toolbox-talks/courses.ts:155-174`.
   - Keep `CreateToolboxTalkCourseItemDto` — it's still live, used by `CreateToolboxTalkCourseDto.items` (the bulk-create path) and structurally matched by `CourseForm`'s `createMutation` call. Confirmed via grep this type has no other dependents that would be orphaned.

2. **Leave the backend `POST /{id}/items` and `DELETE /{id}/items/{talkId}` endpoints alone.** Deleting a public API surface (controller action + MediatR command + handler, potentially a versioning/back-compat conversation) is a materially bigger and riskier decision than trimming an unused frontend hook, and nothing in the recon's brief mandates it — the brief asks specifically about the two frontend hooks. The endpoints are harmless dormant surface: same auth policy as the endpoint that is used, no security delta, no maintenance cost beyond existing. If backend API-surface reduction is wanted later, treat it as its own explicit decision, not a rider on this cleanup.

3. **Edit-route toggle: no change.** Document the "don't gate" conclusion (Part 4) so it doesn't get re-litigated in a future chunk — there's nothing to build.

4. Optionally note the `CourseList.tsx` backslash-path bug (Part 2 aside) to the user/team as a separate, small, unrelated bug fix — not part of this chunk's stated scope, surfaced here only because it was found in the course of this investigation.

This is a small chunk: two hook deletions plus two API-function deletions, no backend changes, no edit-route changes. No new tests are needed (removing unused code doesn't require new coverage), though it would be reasonable to note in the PR description that `POST/DELETE .../items` remain live but frontend-unused, in case a future reader wonders why the controller still has three item-mutation actions when only one is ever called.
