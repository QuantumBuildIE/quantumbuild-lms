# Compose-Existing Course Form — Recon Report

**Date:** 2026-07-09
**Scope:** Read-only investigation. No code changes.
**Purpose:** Establish the actual current state of the "compose existing published talks into a course" model — component, backend, data model, existing data — now that product direction has shifted to make compose-existing the primary course-building model. Prior recons (`docs/course-in-new-wizard-recon.md`, `docs/course-in-new-wizard-impl-first-recon.md`, `docs/course-in-new-wizard-scoping.md`) treated compose-existing as the *rejected* alternative to split-and-author and did not verify it in depth. This recon verifies their claims independently and goes deeper on the parts they skimmed (data integrity, orphaned backend surface, existing data).

---

## Headline

**Substantially built, 2-4 days of gap work to ship** — contingent on two product decisions (Published-only gating for course items; whether compose-existing fully replaces or permanently coexists with the legacy split-and-author flow). The component, its dialogs, and the full CRUD API are real, wired end-to-end, and reasonably production-quality (proper validation, error handling, loading states, no debug artifacts). The only hard blocker to "compose-existing is primary" is that its sole entry point today is a hard redirect to the legacy wizard — a trivial routing fix. The more substantive gap, newly found by this recon and not flagged by the prior docs, is that neither the frontend talk-picker nor the backend create/add-item handlers restrict course membership to **Published** talks — Draft and ReadyForReview talks can be silently added to and then assigned as part of a course.

---

## Part 1 — Component location and characterisation

### 1.1 The component is real and exactly where the prior recon said

`web/src/features/toolbox-talks/components/CourseForm.tsx` (712 lines) is a genuine react-hook-form + zod form. Confirmed by direct read, not assumption. Renders:

- **Course Details** — Title, Description (`CourseForm.tsx:392-428`)
- **Settings** — Active, Sequential Completion, Generate Certificate, Requires Refresher (+ interval, conditional), Auto-Assign to New Employees (+ due days, conditional) (`CourseForm.tsx:430-575`)
- **Learnings** (course items) — `@dnd-kit` (`DndContext`/`SortableContext`/`useSortable`) drag-reorder list, per-item Required toggle, remove button, "Add Learnings" button opening `AddTalksDialog` (`CourseForm.tsx:577-625,670-675`)
- **Assignments** (edit mode only, `{isEditing && course && ...}`) — "Assign Employees" button opening `AssignCourseDialog`, plus `CourseAssignmentsList` (`CourseForm.tsx:627-645,677-683`)

Zod schema (`CourseForm.tsx:70-80`) validates title (1-200 chars), description (≤2000), refresher interval (1-120 months), auto-assign due days (1-365) — real validation, not a stub.

State/hooks: `useCreateToolboxTalkCourse`, `useUpdateToolboxTalkCourse`, `useUpdateCourseItems` (`CourseForm.tsx:207-209`); local `courseItems` state seeded from `course.items` when editing (`CourseForm.tsx:213-230`); `form.watch` for conditional field rendering (`CourseForm.tsx:247-248`).

**API calls** (verified against `web/src/lib/api/toolbox-talks/use-courses.ts` and `courses.ts`):
- Create: `POST /api/toolbox-talks/courses` with `items` inline in the same payload (`courses.ts:136-141`, handler validates and creates items in the same transaction — `CreateToolboxTalkCourseCommandHandler.cs:52-101`)
- Edit: `PUT /api/toolbox-talks/courses/{id}` (metadata) then `PUT /api/toolbox-talks/courses/{id}/items` (full item-array diff) as two sequential mutations (`CourseForm.tsx:304-329`)

**Error handling:** try/catch around submit, axios-error message extraction with fallback to generic message, `toast.error`/`toast.success` (`CourseForm.tsx:302-366`). **Loading states:** `isSaving` disables the submit button and swaps in a spinner + "Saving.../Creating..." label (`CourseForm.tsx:368,649-658`). **Empty state:** dashed-border placeholder when no items added yet (`CourseForm.tsx:618-624`).

**No debug artifacts found.** Grepped `CourseForm.tsx`, `CourseList.tsx`, `CourseAssignmentsList.tsx`, `AddTalksDialog.tsx` for `TODO|FIXME|console\.log|XXX` — zero matches. This is materially more finished than "prototype/stub" — it reads as shipped, reviewed code.

### 1.2 `AddTalksDialog.tsx` — the talk picker

`web/src/features/toolbox-talks/components/AddTalksDialog.tsx` (167 lines). Search-filterable checkbox list, excludes talks already in the course (`excludeTalkIds` prop), multi-select with a "N selected / Clear" affordance, disabled Add button when nothing selected. Calls `useToolboxTalks({ isActive: true, pageSize: 100 })` (`AddTalksDialog.tsx:31-34`).

**Verified gap (new finding, not in prior recons):** this query filters only on `isActive: true` — it does **not** pass `status: 'Published'`. `GetToolboxTalksQueryHandler.cs:45-53` supports an independent `Status` filter that `AddTalksDialog` simply never sets. The dialog visually shows a "Published" badge only when `talk.status === 'Published'` (`AddTalksDialog.tsx:132-134`), implying the author was aware other statuses could appear in the list — but nothing prevents selecting and adding a Draft or ReadyForReview talk. `ToolboxTalk.IsActive` defaults to `true` on every new talk regardless of `Status` (which defaults to `Draft`) — confirmed at `ToolboxTalk.cs:72,77`. Locally, 8 of 15 talks are `Draft` and would all be eligible for the picker. The prior recon's claim that `AddTalksDialog` "lists already-published talks only" does **not** hold — it lists active talks of any status, with Published only distinguished by a badge.

### 1.3 Reachability — genuinely orphaned from creation, confirmed independently

Grepped all of `web/src` for `CourseForm` — 3 hits total: the component file itself, its barrel re-export (`web/src/features/toolbox-talks/components/index.ts:13`), and exactly one importer: `web/src/app/(authenticated)/admin/toolbox-talks/courses/[id]/edit/page.tsx:4`, which renders `<CourseForm course={course} />` inside a Details/Validation tab layout (`edit/page.tsx:44-62`).

There is no second consumer anywhere — confirmed by grepping the barrel export usage too (only the edit page imports from it for this component).

**Creation path today:** `web/src/app/(authenticated)/admin/toolbox-talks/courses/new/page.tsx` is a 5-line unconditional redirect to `/admin/toolbox-talks/create` (the legacy split-and-author wizard) — no query-param branching, no `?wizard=` handling, no conditional logic of any kind. The course list's own "Create New" button (`CourseList.tsx:256-260`) independently routes straight to `/admin/toolbox-talks/create` as well, bypassing `courses/new/page.tsx` entirely. Neither entry point is `wizardPreference`-aware (confirmed by the prior wizard-toggle-retirement recon and re-confirmed here — no `useWizardPreference` import in either file).

**Verdict on "orphaned":** true, and worse than a soft gate — there is no override, hidden route, or flag of any kind that reaches `CourseForm` in create mode. The component itself is fully create-mode-capable (`isEditing = !!course`, `CourseFormProps.course` is optional, the `onSubmit` branch for `!isEditing` correctly calls `createMutation` with items inline — `CourseForm.tsx:203-205,332-351`) — the gap is purely routing, not component capability.

---

## Part 2 — Backend supporting surface

All confirmed by direct read of `src/QuantumBuild.API/Controllers/ToolboxTalkCoursesController.cs` and the CQRS handlers under `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/Courses/`.

| Endpoint | Handler | Auth | Consumed by frontend? | Notes |
|---|---|---|---|---|
| `GET /api/toolbox-talks/courses` | `GetToolboxTalkCoursesQueryHandler.cs` | `Learnings.View` (class) | Yes — `CourseList.tsx` | Search + `isActive` filter; `Result<T>` envelope (`ToolboxTalkCoursesController.cs:52-53`) |
| `GET /api/toolbox-talks/courses/{id}` | `GetToolboxTalkCourseByIdQueryHandler.cs` | `Learnings.View` | Yes — `CourseForm` (edit), `ValidationHistoryTab` | DTO returned directly (not envelope) — `ToolboxTalkCoursesController.cs:84`. Loads sections/questions via `Include`/`ThenInclude` for section/question counts |
| `POST /api/toolbox-talks/courses` | `CreateToolboxTalkCourseCommandHandler.cs` | `Learnings.Manage` (action) | Yes — `CourseForm` create path | Validates title uniqueness per tenant, refresher-interval ≥1, all item talk IDs exist/not-deleted, no duplicate item IDs. **Does not check talk `Status`** |
| `PUT /api/toolbox-talks/courses/{id}` | `UpdateToolboxTalkCourseCommandHandler.cs` | `Learnings.Manage` | Yes — `CourseForm` edit path | Same title-uniqueness + refresher validation, excluding self |
| `DELETE /api/toolbox-talks/courses/{id}` | `DeleteToolboxTalkCourseCommandHandler.cs` | `Learnings.Manage` | Yes — `CourseList.tsx` (`useDeleteToolboxTalkCourse`) | Soft delete, tenant-ownership check throws `UnauthorizedAccessException` |
| `POST /api/toolbox-talks/courses/{id}/items` | `AddCourseItemCommandHandler.cs` | `Learnings.Manage` | **No** — `useAddCourseItem` hook exists (`use-courses.ts:85-95`) but is never imported/called anywhere in `web/src` | **Orphaned backend endpoint.** Validates talk exists + not already in course. No `Status` check |
| `DELETE /api/toolbox-talks/courses/{id}/items/{talkId}` | `RemoveCourseItemCommandHandler.cs` | `Learnings.Manage` | **No** — `useRemoveCourseItem` hook exists (`use-courses.ts:97-107`) but never imported/called | **Orphaned backend endpoint.** `CourseForm` does all add/remove through the bulk items endpoint instead |
| `PUT /api/toolbox-talks/courses/{id}/items` | `UpdateCourseItemsCommandHandler.cs` | `Learnings.Manage` | Yes — `CourseForm` (both create-adjacent edit flow and the only add/remove/reorder path actually used) | Full array diff: soft-deletes items not in new payload, updates `OrderIndex`/`IsRequired` on survivors, inserts new. Validates all talk IDs exist, no duplicates. **No `Status` check** |

### 2.1 Course Assignment surface (already-consumed, verified working)

`ToolboxTalkCourseAssignmentsController` (`api/toolbox-talks/course-assignments`) backs `AssignCourseDialog`/`CourseAssignmentsList` — `POST .../preview`, `POST /`, `GET .../by-course/{courseId}`, `GET .../{id}`, `DELETE .../{id}` all have live frontend callers (`web/src/lib/api/toolbox-talks/course-assignments.ts`). `AssignCourseCommandHandler.cs:46-55` checks `course.IsActive` and that the course has ≥1 non-deleted item, but **does not check the underlying `ToolboxTalk.Status`** of any course item — a course entirely composed of Draft talks can be assigned to employees today, with no server-side guard.

### 2.2 No other course-related backend-only scaffolding found

Grepped `web/src` for `course-assignments/preview`, `validation-runs`, `content-options` — the course-scoped `TranslationValidationRun` surface (`ValidationHistoryTab.tsx:78-79`, course-scoped run detail page at `courses/[id]/validation/[runId]/page.tsx`) is live and consumed. `content-options` belongs to the unrelated Requirement Mapping feature, not course composition. Aside from the two orphaned single-item endpoints above (§2, rows 6-7), everything else in the Course CRUD/Assignment surface has a real frontend caller.

### 2.3 Response convention note

`GET /courses` returns a `Result<T>` envelope (frontend reads `response.data.data` — `courses.ts:127-128`); every other course endpoint (`GetById`, `Create`, `Update`, `AddItem`, `RemoveItem`, `UpdateItems`) returns the DTO directly (frontend reads `response.data`). This matches CLAUDE.md Note 18's documented split and is handled correctly on the frontend — not a bug, just worth knowing before writing new calls against this controller.

### 2.4 Aside: CLAUDE.md permission table drift

CLAUDE.md's endpoint table documents the Courses endpoints under `ToolboxTalks.Create/Edit/Delete`. The actual code enforces `Learnings.View` (class-level) / `Learnings.Manage` (action-level) — confirmed at `ToolboxTalkCoursesController.cs:17,97,128,165,200,237,269` and `Permissions.cs:14-17`. Not a functional problem (both permission sets exist and are assignable), just a documentation-vs-code mismatch worth a note for whoever next touches CLAUDE.md's endpoint table.

---

## Part 3 — Data model assessment

Read directly from `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/` and their EF configurations.

- **`ToolboxTalkCourse`** (`TenantEntity`) — `Title`, `Description`, `IsActive`, `RequireSequentialCompletion`, `RequiresRefresher`, `RefresherIntervalMonths`, `GenerateCertificate`, `AutoAssignToNewEmployees`, `AutoAssignDueDays`. **No `Status`/draft-publish state machine of its own** — `IsActive` (bool) is the only lifecycle flag. This is a real design characteristic worth flagging: under compose-existing, "create" and "make assignable" are the same action (a course is assignable the moment it's created with `IsActive = true`, the default). There is no course-level draft/review/publish gate analogous to `ToolboxTalk.Status`.
- **`ToolboxTalkCourseItem`** (`BaseEntity`, join) — `CourseId`, `ToolboxTalkId`, `OrderIndex`, `IsRequired`. **This is a pure reference — no embedded content, no section-index-style fields, nothing shaped for split-and-author.** Confirmed by full read (`ToolboxTalkCourseItem.cs:9-19`) — the entity is already exactly compose-existing-shaped. FK to `ToolboxTalk` is `DeleteBehavior.Restrict` (a talk referenced by a course item cannot be hard-deleted out from under it — soft-delete is the only path, per the tenant-wide soft-delete convention). Unique index on `{CourseId, ToolboxTalkId}` prevents duplicate membership (`ToolboxTalkCourseItemConfiguration.cs:64-67`).
- **`ToolboxTalkCourseTranslation`** (`BaseEntity`) — `CourseId`, `LanguageCode`, `TranslatedTitle`, `TranslatedDescription`. Course-level title/description translation only; item-level talks carry their own full `ToolboxTalkTranslation` (sections, quiz, etc.) independently. Under compose-existing this is arguably sufficient by construction, since items are (in principle) already-validated standalone talks — but see Part 5 for the gap this creates if Draft/untranslated talks slip through the missing Status gate.
- **`ToolboxTalkCourseAssignment`** (`TenantEntity`) — confirmed wizard-agnostic per prior recon; unaffected by which authoring model produced the course.

**Conclusion:** no schema changes are needed to support compose-existing — the entity shape already assumes reference-by-ID composition, not embedded/owned content. This is consistent with (and independently reconfirms) the prior recon's Part 5 finding.

---

## Part 4 — Existing course data

**Database was reachable — local Dev Postgres at `127.0.0.1:5432/rascor_stock`, connected via `psql` (found at `C:\Program Files\PostgreSQL\17\bin\psql.exe`), empty password matching `appsettings.Development.json`'s connection string.** Results below are real query output, not fabricated:

```
SELECT COUNT(*) FROM toolbox_talks."ToolboxTalkCourses";                    -- 0
SELECT COUNT(*) FROM toolbox_talks."ToolboxTalkCourses" WHERE "IsDeleted"=false;  -- 0
SELECT COUNT(*) FROM toolbox_talks."ToolboxTalkCourseItems";                -- 0
SELECT COUNT(*) FROM toolbox_talks."ToolboxTalkCourseItems" WHERE "IsDeleted"=false; -- 0
SELECT COUNT(*) FROM toolbox_talks."ToolboxTalkCourseAssignments";          -- 0
SELECT COUNT(*) FROM toolbox_talks."ToolboxTalkCourseTranslations";         -- 0
```

Per-tenant distribution — all 7 tenants (RASCOR + 6 Playwright E2E tenants) show **zero** courses:

| Tenant | Course count |
|---|---|
| E2E Tenant 1781963767555 | 0 |
| E2E Tenant 1781964097229 | 0 |
| E2E Tenant 1782138856198 | 0 |
| RASCOR | 0 |
| E2E Tenant 1782138859264 | 0 |
| E2E Tenant 1781963764736 | 0 |
| E2E Tenant 1781964094748 | 0 |

**No course has ever been created locally, by either wizard.** This is genuinely different from "DB unreachable" — the DB is reachable and the tables exist and are correctly structured, they are simply empty. Part 9's "inspect existing courses for split-and-author vs. compose origin" is therefore **not answerable from local data** — there is nothing to sample. (For context: `ToolboxTalks` itself does have local data — 15 talks: 8 Draft, 6 Published, 1 ReadyForReview — but none are linked to any course.)

This has a direct implication for Part 5/6: there is no local regression risk from existing course rows (nothing to break), but it also means the "does compose-existing data already look organically composed vs. AI-split" question has no evidence either way locally. If a real signal is needed, it would have to come from the Railway Development or Production database, neither of which was reachable this session (consistent with the wizard-toggle-retirement recon's same finding — Railway CLI token was not available in that session either; not re-attempted here since this recon's scope is local-only per the task).

---

## Part 5 — Gap analysis

Given the product direction that compose-existing becomes primary:

### Frontend gaps

1. **No creation route.** `courses/new/page.tsx` is a hard redirect to the legacy wizard (`new/page.tsx:1-5`). Needs to render `<CourseForm />` (no `course` prop) instead. Trivial — the component already handles this mode correctly.
2. **"Create New" button routes wrong.** `CourseList.tsx:257` pushes to `/admin/toolbox-talks/create` unconditionally. Needs to point at the new create route once it exists.
3. **No Published-only filtering in `AddTalksDialog`.** Confirmed gap (§1.2) — the picker can surface and let an admin add Draft/ReadyForReview talks to a course. Needs either a `status: 'Published'` param added to the `useToolboxTalks` call, or an explicit product decision that non-Published talks are allowed (unlikely, given they may have no sections/quiz content, no translations, and haven't passed TransVal).
4. **No visual state for "this course has non-Published items"** if the above is intentionally left permissive at the picker level — no warning banner anywhere in `CourseForm` today if items end up Draft.
5. **No course-level Draft/Publish workflow** — since `ToolboxTalkCourse` has no `Status` field, "create" and "assignable" are the same moment. Whether this is acceptable for the primary course-building model, or whether a lightweight review/publish gate is wanted (mirroring `ToolboxTalk.Status`), is an open product question, not a coding gap per se.
6. **Wizard-toggle independence.** Neither `courses/new` nor the list's Create button reads `useWizardPreference` today. If compose-existing becomes primary but split-and-author (via the legacy wizard's Course output type) is meant to survive as a secondary option, a product decision is needed on how a user chooses between them at the entry point — right now there is exactly one entry point and it goes to the wrong (legacy) flow.

### Backend gaps

1. **No `Status == Published` guard** in `CreateToolboxTalkCourseCommandHandler` (item validation, `CreateToolboxTalkCourseCommandHandler.cs:56-66`) or `UpdateCourseItemsCommandHandler` (`UpdateCourseItemsCommandHandler.cs:40-54`) — both only check the talk exists and isn't soft-deleted.
2. **No `Status == Published` guard in `AssignCourseCommandHandler`** (§2.1) — a course with unpublished items can be assigned to employees today.
3. Two backend endpoints (`AddCourseItemCommandHandler`, `RemoveCourseItemCommandHandler`) are dead weight from the frontend's perspective — not broken, just unused; worth knowing they exist in case a future single-item-add UI wants them, or they should be pruned for surface-area hygiene.

### UX gaps

- No indicator anywhere (list, detail, or item picker) of *why* a talk is or isn't eligible for a course beyond the "Published" badge in the picker, which is purely informational, not a filter.
- No bulk "publish all items then assign course" workflow if Published-gating is added — an admin composing a course from Draft talks would currently discover the problem (if the gate is added) only at save time via a generic 400, not proactively in the picker.

### Integration gaps

- **Course-level scheduling:** unaffected either way — `AssignCourseCommandHandler` fans out into `ScheduledTalk` rows regardless of which UI created the course, matching the prior recon's "wizard-agnostic" finding, independently reconfirmed here by direct read.
- **Course-level translation:** `ToolboxTalkCourseTranslation` (title/description only) is populated exclusively through the manual translate flow gated on `ToolboxTalksController`'s translation-generation endpoints for talks, not courses — confirmed no course-level translation trigger exists in the Courses controller (no `/translations` route on `ToolboxTalkCoursesController`). If compose-existing courses need their own title/description translated, that has to be triggered separately today; not wired into `CourseForm`'s save flow at all.
- **Course-level validation (`TranslationValidationRun.CourseId`):** already fully wired for *display* (`ValidationHistoryTab`, course-scoped run detail page) but nothing *populates* a course-scoped run except the legacy `PublishAsCourseAsync`'s reassociation step. Under compose-existing, since items are (intended to be) already-validated standalone talks, this course-level validation concept would sit permanently empty for every composed course — worth an explicit product decision on whether that's fine (validation lives at the item level only) or whether a course-level "roll-up" view/report is wanted.
- **External review:** confirmed (again) to have no course concept anywhere — consistent with both prior recons.

---

## Part 6 — Scope estimate

**2-4 days**, assuming the product decisions below resolve quickly:

- **~0.5 day** — creation route + button wiring (Gaps 1-2). Mechanical; the component is already correct.
- **~0.5-1 day** — Published-only gating, frontend + backend (Gaps 3, Backend 1-2), assuming the decision is "yes, gate to Published." If the decision is "allow non-Published but warn," this could take slightly longer (needs a warning UI state that doesn't exist today).
- **~0.5 day** — regression pass: verify assignment, scheduling, certificates, and employee-facing experience are unaffected for courses created via the new route (expected to hold, since none of that machinery branches on authoring path — confirmed by direct read of `AssignCourseCommandHandler`, `ToolboxTalkCourseAssignment`, and the fact that `ToolboxTalkCourseItem` is a pure ID reference).
- **~0.5-1.5 days buffer** — for whichever product decisions (Draft/Publish state for courses; coexistence with the legacy split-and-author flow; course-level translation trigger UX) turn out to need more than a routing/filter fix once actually scoped.

This is a **much smaller lift than the split-and-author path** the earlier recons scoped at 4-6 weeks — the entire reason being that compose-existing's hard parts (form, picker, drag-reorder, CRUD API, assignment integration) are already built and only need a front door. The earlier scoping doc's own estimate of "~2 weeks" for this path (`docs/course-in-new-wizard-scoping.md:23`) reads as conservative in light of this recon's direct verification — the actual remaining work is closer to a few days than two weeks, *provided* the Published-only gating decision doesn't cascade into a larger "what does an unpublished course item mean" design exercise.

**Which route structure fits better:** a standalone route under the existing `admin/toolbox-talks/courses/` tree (e.g. `courses/new` rendering `CourseForm` directly) fits far more naturally than shoehorning this into the `learnings/**` new-wizard's step-per-URL, single-`ToolboxTalk`-row architecture. The impl-first recon (`docs/course-in-new-wizard-impl-first-recon.md`) already established that the new wizard's entire architecture assumes one `ToolboxTalk` row as wizard state from Step 1 — that assumption doesn't apply here at all, since compose-existing never authors new talk content, it only references existing ones. Forcing this into the `learnings/**` step-shell would add complexity (a step framework, URL-per-step navigation) for a workflow that is naturally a single form. Recommend keeping `CourseForm` as a standalone page, not a wizard step.

---

## Notes for the boss

1. **This reverses the locked decision in `docs/course-in-new-wizard-scoping.md`.** That doc explicitly rejected compose-existing ("the trade-off — losing the 'upload your manual, get a course' flow — was judged unacceptable") in favor of split-and-author, and locked a 4-6 week plan around porting `PublishAsCourseAsync` into the new wizard. If compose-existing is now primary, that entire plan (and the "additive parallel track" framing built around it) needs to be explicitly superseded, not just quietly abandoned — worth a short follow-up decision doc analogous to the scoping doc, stating what happens to split-and-author: retired, kept as a secondary "AI-author" option, or something else.
2. **The Published-only gating gap is a real, verified data-integrity hole, not a hypothetical.** Nothing today stops an admin from composing and then assigning a course made entirely of Draft talks with zero content. This should be closed before compose-existing becomes the primary, promoted path — right now it's a low-traffic edge case because the feature is orphaned and no one has found it by accident.
3. **No local course data exists to learn from.** Zero courses across all 7 local tenants. If there's a real trial-customer or Development/Production dataset with actual course usage, checking whether any courses there were composed vs. split-and-authored would be a useful sanity check before finalizing the "primary model" decision — this recon could not do that check locally.
4. **The course entity's lack of its own Draft/Publish state** is a design point worth a deliberate decision now rather than discovering it later: today, saving a course *is* publishing it (assignable immediately, since `IsActive` defaults `true`). That may be exactly right for a "quickly assemble from existing vetted content" workflow — or it may need a review gate if course composition becomes a primary, frequently-used path rather than an occasional edit-time action.
