# Course creation — scoping v2

## Purpose

Supersedes `docs/course-in-new-wizard-scoping.md`. This document
captures the strategic decisions for shipping compose-existing course
creation, following the reframe that separated course composition from
AI-assisted content splitting.

Written after `docs/compose-existing-form-recon.md` established that a
substantially complete compose-existing form component (`CourseForm.tsx`)
already exists in the codebase, requiring only entry-point wiring,
one bug fix, and dormant-code gating to ship.

## Product model

Two concepts, clearly separated:

- **Learnings** are coherent training units authored deliberately via
  the new wizard. Reusable, editable, first-class content entities.
  This is the shipped model — no changes needed on the learnings side.
- **Courses** are ordered compositions of existing learnings, with
  shared metadata (title, sector, assignment settings, refresher
  cadence). Created by picking learnings from a searchable list,
  ordering them via drag-and-drop, setting course metadata, and
  publishing.

Course creation is pure composition. No content generation happens
in the course creation flow.

## What this replaces

The legacy course flow ("upload one document, AI splits it into
sections, each becomes a talk, all bundled into a course") is retired
from the primary path. It is gated behind a toggle mirroring the
`UseNewWizard` pattern; legacy code remains untouched in the codebase.
Cleanup of the legacy course code is deferred to a future release.

The AI-assisted document splitting capability that was bundled into
the legacy course flow is not preserved in the new path. Deferred as
a possible future learnings-side feature (v2 or later). Watch for
customer demand before rebuilding.

## Strategic decisions locked

### Model: compose-existing

Course = composition of existing learnings. See `Product model` above.

### Track: additive parallel via toggle

Legacy course path stays dormant behind `UseNewCourseCreation`
tenant setting. New path renders when the toggle is on. URL override
(`?coursemode=old`) provides per-navigation escape hatch. Cleanup pass
to delete legacy course code is separate work, deferred.

### Existing course data: void at cutover

Trial customer's existing legacy courses (if any) are voided at
cutover. They are informed. No migration built. No coexistence logic.
Development and Demo course data can be wiped as part of release prep.

Rationale: legacy course data volume is trivially small (0 rows in
local Dev; trial customer is the only tenant likely to have any).
Migration cost far exceeds the value of preserving the data.

### AI splitting: deferred

Not part of this release. If customer demand surfaces, rebuild as a
learnings-side feature ("upload a document, get N draft learnings you
can edit or discard, then compose them into a course separately").
Not before.

### Production strategy: unified cutover

`UseNewWizard` and `UseNewCourseCreation` flip together at the
Production release. Trial customer experiences both wizard-cutover
and course-cutover simultaneously — cleaner than staggered flips.

Everything else waiting on `transval` (external review refactor,
refresh flow fixes, item #1 slideshow, item #6 review mode, item #4
all chunks) ships in the same release.

## Timeline

2-4 days of implementation, per `docs/compose-existing-form-recon.md`.

Not weeks. This is a small, defined piece of work.

## Implementation shape

The recon identified the following gaps:

**Route wiring:**
- `courses/new/page.tsx` currently hard-redirects to the legacy
  wizard. Route the read of `UseNewCourseCreation` and either render
  `CourseForm` (new path) or redirect (legacy path).
- Course-list "Create New" button follows the same gate.

**Published-status guard (bug fix, would apply regardless):**
- `AddTalksDialog.tsx` filters talks on `isActive: true` only, not on
  `status: Published`. Extend the filter.
- Backend course-create and course-item-add handlers don't check talk
  status either. Add a guard that rejects Draft talks from being
  added to a course.

**Dead-code hooks investigation:**
- `useAddCourseItem` and `useRemoveCourseItem` exist as hooks but
  aren't called anywhere. Verify whether they represent intended
  post-publish item management, or dead code from an earlier
  refactor. If intended, wire them; if dead, delete them.

**Course setting toggle:**
- Add `UseNewCourseCreation` tenant setting following the
  `UseNewWizard` pattern.
- Default value at cutover: true.

**URL override:**
- `?coursemode=old` per-navigation override, mirroring `?wizard=old`.

**Testing:**
- Integration test for the new path: create course, add talks,
  reorder, publish, assign.
- Regression test for the legacy path continuing to work behind the
  toggle.

## Chunks

Provisional, to be refined by the implementation recon:

1. **Route wiring + toggle setup.** Entry-point fixes, toggle
   plumbing, URL override. No user-visible behaviour change on the
   old path; new path becomes reachable when toggle is on.
2. **Published-status guard.** Bug fix. Applied regardless of which
   course path is active — the legacy path also benefits from
   catching Draft talks.
3. **Dead-code hooks resolution.** Investigation + wire or delete.
4. **Testing and smoke.** Integration test coverage + Development
   walk-through.

## Cutover coordination

At Production release:
- Flip `UseNewWizard = true` (talks use new wizard)
- Flip `UseNewCourseCreation = true` (courses use new form)
- Ship everything from `transval` in one coordinated release
- Trial customer notified before release, existing legacy courses
  voided at their acknowledgement

## What is not in scope

- Deleting the legacy course code (deferred cleanup pass)
- Migrating existing legacy courses (voided instead)
- AI-assisted document splitting (deferred; possibly future
  learnings-side feature)
- Any change to the shipped new wizard for talks
- Any change to how courses are consumed by operators (viewing,
  completion, certificates, review mode — all wizard-agnostic per
  `docs/course-in-new-wizard-recon.md`)

## Notes for future context

- The reasoning trail leading to this direction is in
  `docs/course-in-new-wizard-recon.md` (initial state assessment) and
  `docs/compose-existing-form-recon.md` (compose-existing form
  characterisation). Also see `docs/navigation-entry-points-recon.md`
  for the entry-point-context analysis that helped surface the
  reframe.
- The superseded v1 scoping doc (`docs/course-in-new-wizard-scoping.md`)
  is preserved with a supersession header for reasoning-trail
  context. Do not act on its recommendations.