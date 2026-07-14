# Recon: Learning and Courses Navigation Entry Points

**Date:** 2026-07-09
**Scope:** Read-only investigation. No code changes.
**Feeds into:** `docs/course-in-new-wizard-impl-first-recon.md` (Q3 — where does the Lesson/Course choice live in the new wizard's navigation?)

---

## 1. Executive summary

The working assumption going in was: *an admin who wants a course enters through a distinct "Courses" section of the nav, separate from "Learnings," so the course-vs-standalone decision might already be implicit in their navigation choice by the time they reach the wizard.*

**This assumption is false for the current codebase.** There genuinely are two separate nav tabs, two separate landing pages, and two separate list components (`ToolboxTalkList` for Learnings, `CourseList` for Courses) — that part of the assumption holds. But **creation** does not follow that separation. Both "Create New" buttons converge on `/admin/toolbox-talks/create`, the same legacy `CreateWizard`, and neither button passes any query parameter, flag, or context that tells the wizard which one the admin meant. The Lesson-vs-Course decision is made entirely *inside* the wizard, on the Parse step, via the `OutputTypeSelector` UI — identically regardless of which nav tab the admin arrived from. An admin who clicks "Courses → Create New" lands in the exact same screen as one who clicks "Learnings → Create New," and both are asked to pick "Single Lesson" or "Course" only after they've uploaded content and it's been parsed.

There is a second, sharper asymmetry: the **new wizard** (`/admin/toolbox-talks/learnings/new`) is only reachable from the Learnings entry point, and only when the tenant's `UseNewWizard` setting is on. The Courses "Create New" button has no wizard-preference check at all — it hardcodes the legacy route. So today, a tenant fully cut over to the new wizard for standalone talks still gets routed to the *old* wizard the moment they try to create anything from the Courses page. This is not a new-wizard course gap specifically — it's that Courses creation hasn't been wired to the wizard-preference system at all yet, for either wizard.

Net implication for the design question this recon was commissioned to answer: **the entry point does not currently carry the course-vs-standalone signal**, so building the new wizard to trust "which button was clicked" would be inventing a distinction the codebase doesn't currently make. See §7 for the full reasoning and §8 for a recommendation.

---

## 2. Full navigation enumeration

There is no sidebar in this app — navigation is a top `<header>` (`TopNav`) plus two nested layers of in-page tab strips.

### 2a. Top nav dropdown (`web/src/components/layout/top-nav.tsx`)

Gated by `useHasAnyPermission(["Core.ManageEmployees", "Core.ManageUsers", "Learnings.Manage", "Learnings.Schedule", "Learnings.Admin", "LessonParser.Use"])`.

| Label (verbatim) | Route | Condition |
|---|---|---|
| **Administration** | `/admin` | Default, for anyone with admin-level permissions |
| **Learning Management** | `/admin/toolbox-talks` | Supervisor-only users (same permission gate, different label) |

### 2b. Admin top-level tabs (`web/src/app/(authenticated)/admin/layout.tsx`, `adminNavItems`)

| Label | href | Gate |
|---|---|---|
| Employees | `/admin/employees` | `Core.ManageUsers` or `Core.ManageEmployees` |
| Users | `/admin/users` | `Core.ManageUsers` |
| **Learnings** | `/admin/toolbox-talks` | `Learnings.View`/`Manage`/`Schedule`/`Admin` |
| Settings | `/admin/settings` | `Learnings.Admin` or `Core.ManageUsers` |
| Lesson Parser | `/admin/lesson-parser` | `LessonParser.Use` |
| Tenant Management | `/admin/tenants` | SuperUser only |
| Regulatory | `/admin/regulatory` | `Learnings.Admin` |
| Monitoring | `/admin/monitoring` | SuperUser only |

Note: at this level the entire Toolbox Talks admin area — talks, courses, schedules, assignments, reports, certificates, settings — is behind a **single tab labeled "Learnings"**. "Courses" does not appear here; it's one level deeper.

### 2c. Toolbox Talks section tabs (`web/src/app/(authenticated)/admin/toolbox-talks/layout.tsx`, `adminToolboxTalksNavItems`)

This is where "Learnings" and "Courses" become siblings:

| Label | href |
|---|---|
| Overview | `/admin/toolbox-talks` |
| **Learnings** | `/admin/toolbox-talks/talks` |
| **Courses** | `/admin/toolbox-talks/courses` |
| Schedules | `/admin/toolbox-talks/schedules` |
| Assignments | `/admin/toolbox-talks/assignments` |
| Reports | `/admin/toolbox-talks/reports` |
| Certificates | `/admin/toolbox-talks/certificates` |
| QR Locations | `/admin/toolbox-talks/qr-locations` (`Learnings.Admin`) |
| Pipeline Audit | `/admin/toolbox-talks/pipeline` |
| Settings | `/admin/toolbox-talks/settings` (`Learnings.Admin`) |

A special case in the active-tab logic reroutes the new wizard's URL space back onto the "Learnings" tab even though it lives under a different folder:

```tsx
// The learning wizard lives at /admin/toolbox-talks/learnings/** but belongs
// to the Learnings tab (which links to /admin/toolbox-talks/talks).
if (href === '/admin/toolbox-talks/talks' && pathname.startsWith('/admin/toolbox-talks/learnings')) {
  return true;
}
```

### 2d. Other creation flows (brief mention)

- **Schedules** (`/admin/toolbox-talks/schedules/new`) — assigns existing talks/courses to employees on a cadence; not a content-creation flow.
- **Employees / Sites / Companies / Users** (`/admin/employees/new` etc.) — Core module CRUD, unrelated to the wizard question.
- **Bulk employee import** (`/admin/employees/bulk-import`) — separate multi-step flow, not analogous to talk/course creation.

None of these interact with the Lesson/Course decision.

---

## 3. Learnings entry-point trace

1. **Landing page:** `web/src/app/(authenticated)/admin/toolbox-talks/talks/page.tsx` — renders `<ToolboxTalkList>`, page heading "Learnings." Route `/admin/toolbox-talks/talks`.
2. **What the admin sees:** a `DataTable` of existing talks (list component `ToolboxTalkList.tsx`) with search/filter and a "Create New" button, gated by `canManage`.
3. **Create New button** resolves its destination via `useWizardPreference()`:

```tsx
const wizardPreference = useWizardPreference();
...
<Button onClick={() => router.push(
  wizardPreference === 'new'
    ? '/admin/toolbox-talks/learnings/new'
    : `${basePath}/create`
)}>
  Create New
</Button>
```

4. **`useWizardPreference()`** (`web/src/features/toolbox-talks/hooks/useWizardPreference.ts`) resolves, in order: a one-shot `?wizard=new|old` URL override → the tenant's persisted `UseNewWizard` setting → default `'old'`.
5. **No context passed on navigation.** Whichever wizard is launched, the `router.push` call carries no query params, no state, nothing indicating "this should default to Lesson" — the wizard starts cold either way.
6. Legacy wizard's own "Create Another" button on the Publish step (`PublishStep.tsx`) uses the identical `useWizardPreference()` pattern to decide where to send the admin next — reinforcing that wizard choice is a **tenant-wide setting**, not something scoped per-creation-attempt.

**Which wizard launches:** either, tenant-setting-dependent. **Context passed:** none.

---

## 4. Courses entry-point trace

1. **Landing page:** `web/src/app/(authenticated)/admin/toolbox-talks/courses/page.tsx` — renders `<CourseList>`. Route `/admin/toolbox-talks/courses`.
2. **What the admin sees:** a `DataTable` of existing courses (see §5 for columns/actions) with a "Create New" button, gated by `canManage`.
3. **Create New button** — no wizard-preference check at all:

```tsx
{canManage && (
  <Button onClick={() => router.push('/admin/toolbox-talks/create')}>
    Create New
  </Button>
)}
```

Always routes to the **legacy** `CreateWizard`, unconditionally, regardless of the tenant's `UseNewWizard` setting.

4. **`courses/new/page.tsx`** exists as a route but is dead weight relative to the button above — it's a pure redirect stub that nothing currently links to:

```tsx
export default function LegacyNewCoursePage() {
  redirect('/admin/toolbox-talks/create');
}
```

It happens to redirect to the same place the button already goes directly, so behavior is consistent, but the page itself is unreachable via any in-app click path found.

5. **Where Lesson vs Course actually gets decided:** inside the legacy wizard's Parse step, via `OutputTypeSelector.tsx` — a two-card picker:

```tsx
const OUTPUT_OPTIONS = [
  { type: 'Lesson', label: 'Single Lesson', description: 'All sections combined into one toolbox talk' },
  { type: 'Course', label: 'Course', description: 'Each section becomes a separate lesson in an ordered course' },
];
```

This is the **same component, same step, same wizard instance** an admin would reach from the Learnings entry point. Nothing about having arrived via "Courses → Create New" pre-selects the Course card, skips the picker, or even visually highlights it as the presumed choice. (The prior recon, `course-in-new-wizard-impl-first-recon.md` §2.1, separately found that the "AI Suggested" badge on this picker is also non-functional — `SuggestOutputType` always returns `Lesson`.)

6. **New wizard has no course entry point, confirmed.** `grep` across `web/src/app/(authenticated)/admin/toolbox-talks/learnings/**` and `web/src/features/toolbox-talks/components/learning-wizard/` for course-related code returns nothing except one unrelated field, `isPartOfCourse: false`, which is read-only metadata on an *already-created* talk (used to show "this talk belongs to a course" on `ToolboxTalkDetail.tsx`), not a creation-time flag. This confirms the prior recon's finding still holds: zero course-creation code exists in the new wizard, and — new finding this pass — the Courses list doesn't even attempt to route there.

**Which wizard launches:** always legacy. **Context passed:** none.

---

## 5. Existing course management surfaces

**Courses landing page** (`CourseList.tsx`, via `courses/page.tsx`):
- Search box + Status filter (Active/Inactive)
- Columns: Title (+ description), Learnings (item count), Sequential (Yes/No badge for `requireSequentialCompletion`), Translations (count), Status, Created date, Actions
- Row actions (gated by `canManage`): **Edit** → `/admin/toolbox-talks/courses/{id}/edit`, **Delete** (confirmation dialog)
- Notably **no "View" and no "Publish" row action**, unlike the Learnings list which has View/Edit/Schedule/Delete — courses can only be edited or deleted from this table, not previewed standalone
- Empty state copy: *"No courses found. Create your first course to group learnings together."* — this is the one place the UI states an explicit mental model in words ("group learnings together"), see §6.

**Post-creation management** (file references only, not deep-dived per scope):
- `web/src/app/(authenticated)/admin/toolbox-talks/courses/[id]/edit/page.tsx` — tabs "Details" / "Validation"
- `web/src/features/toolbox-talks/components/CourseForm.tsx` — drag-reorder of course items via `@dnd-kit`, required/sequential toggles, add-talks picker
- `web/src/app/(authenticated)/admin/toolbox-talks/courses/[id]/validation/[runId]/page.tsx` — course-level validation run detail
- Employee-facing: `web/src/app/(authenticated)/toolbox-talks/courses/[id]/page.tsx`

---

## 6. Conceptual distinction: Learnings vs Courses

**For browsing/managing existing content, the separation is clean:** two nav tabs, two landing pages, two list components, two data shapes (`ToolboxTalkListItem` vs `ToolboxTalkCourseListDto`), two edit surfaces. An admin looking for "the course I built last week" goes to Courses; looking for "the talk on ladder safety" goes to Learnings. No crossover in this direction.

**For creation, the separation is not clean — it's a shared flow with entry-point framing that doesn't actually do anything.** Both "Create New" buttons are, in effect, doors into the same room (`/admin/toolbox-talks/create`), and the room itself asks the question the doors implied was already answered. This matches the recon prompt's "shared underlying flow with different entry-point framing" pattern, except even the "framing" part is thin — the Courses button doesn't pass a mode flag, so it isn't framing the wizard's behavior at all, just its own landing page's copy and empty-state text.

**Is this current behavior or a considered target?** Current behavior only, as far as this recon can establish from the code — there's no comment, flag, or dead code suggesting the button was ever wired to pass an `outputType=Course` hint and had it removed. It reads as two features (the Courses list UI, and the wizard's OutputType picker) that were each built to be self-sufficient, without the former informing the latter. The prior recon's finding that `SuggestOutputType` always returns `Lesson` (a stub never finished) is consistent with this being an area of the app that was left partially wired.

**What is the admin's mental model?** This recon can infer the *labels* the admin sees but not their mental model with confidence, per the instruction to not guess where evidence is thin. What the UI text supports:
- The Courses empty state ("group learnings together") frames a Course as a container *composed of* Learnings, not a separate content type — consistent with the data model (`ToolboxTalkCourseItem` joins to `ToolboxTalk` rows).
- The wizard's own picker copy — "Single Lesson" vs "Course: each section becomes a separate lesson in an ordered course" — reinforces the same "course = many lessons" framing, but uses "Lesson" where the Courses list UI uses "Learnings" and the data layer uses "Talk."
- Whether an admin who clicks "Courses → Create New" *expects* to skip a Lesson/Course question they feel they already answered, versus expecting the wizard to ask them what to build with their uploaded material, is not something the code can answer. Flagging this as an open question rather than asserting an answer.

---

## 7. Implication for the course-vs-standalone decision timing

Given §3–§6, asking the admin to choose Lesson vs Course *inside* the wizard is, in the current codebase, **necessary — not redundant** — because the entry points do not distinguish. Specifically:

- Neither "Create New" button passes any signal the wizard could use to skip the question.
- The Courses button doesn't even reach a different wizard instance or route — it's the identical `/admin/toolbox-talks/create` URL, same component tree, as the Learnings legacy path.
- If the in-wizard question were removed on the assumption that "the admin already told us via which button they clicked," that assumption would have to be built fresh — it doesn't exist today to inherit.

There is a secondary, narrower case for **partial** redundancy worth naming for the design decision: even without changing the entry-point wiring, the Courses page's "Create New" button *could* pass a hint (e.g., a query param or initial-state flag) causing the wizard to pre-select the Course card while still leaving it changeable — matching the recon prompt's "somewhere in between" pattern (usually redundant, but the AI-suggested override or a change-of-mind mid-flow still needs to be possible). That would be a genuinely new piece of wiring, not something to assume already happens.

This has a direct bearing on `course-in-new-wizard-impl-first-recon.md` Q3 (does the Lesson/Course choice get a distinct entry route, e.g. `learnings/course/new`, or live inside the existing `learnings/new` flow?). That recon recommended a distinct entry route as a consequence of the Shape A architecture decision (deferred `ToolboxTalk` creation for the course path). This recon adds a supporting fact: a distinct route wouldn't be inventing a new pattern out of nothing — the Courses nav tab and landing page already establish "Courses" as its own place in the admin's mental map, even though the current *creation* flow doesn't yet route through it distinctly. Wiring `CourseList.tsx`'s "Create New" button to a real course-specific route (new-wizard-aware, unlike today's hardcoded legacy link) would be a natural, low-risk companion change alongside the new-wizard course build — and would also close the separate gap noted in §4 (Courses creation currently ignores `UseNewWizard` entirely, even for standalone-talk-equivalent tenants who've fully cut over).

---

## 8. Recommended design principle for the new wizard

Treat entry point and in-wizard choice as two independent signals rather than one implying the other:

1. **Give Courses its own creation route** (per the prior recon's Q3 recommendation), wired through `useWizardPreference()` the same way Learnings already is — closing the asymmetry in §4 where Courses ignores the tenant's wizard setting entirely.
2. **Let the entry route set the *default*, not the *only* option.** Arriving via Courses pre-selects "Course" (once that concept exists in the new wizard); arriving via Learnings pre-selects "Single Lesson" (or skips the picker entirely, since Learnings is the new wizard's only supported mode today). Either way, keep the choice changeable — the recon prompt's "necessary" case (admin changes their mind mid-flow, or the parsed content doesn't split the way they expected) is a real scenario neither this recon nor the prior one found a reason to dismiss.
3. **Don't let this recon's finding — "the entry point currently carries no signal" — be read as "the entry point shouldn't carry a signal."** It only shows that no such wiring exists *yet*; building it is squarely in scope for the course-in-new-wizard work and is a small addition once the Q1 architecture decision (session-holder shape) is settled, since the entry route is the natural place to instantiate whichever state holder that decision produces.
