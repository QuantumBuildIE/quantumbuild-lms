# Course-in-new-wizard — scoping and strategy

> **⚠️ Superseded by `docs/course-in-new-wizard-scoping-v2.md` (2026-07-16).**
>
> This document locked in the "split-and-author" model and a 4-6 week
> timeline. That decision was reversed after further discussion
> surfaced that split-and-author was itself a demo-pressure compromise
> that conflated two separate capabilities (course composition and
> AI-assisted content splitting), and that a substantially complete
> compose-existing form component already exists in the codebase.
>
> The v2 doc reflects the corrected direction: compose-existing as the
> primary course model, with the legacy split-and-author path gated
> behind a toggle mirroring the wizard-cutover pattern. Timeline
> revised to 2-4 days.
>
> Preserved here for reasoning-trail context. Do not act on
> recommendations in this document.


## Purpose

This document captures the strategic decisions made about porting course
creation from the legacy wizard to the new wizard. Written after the
recon at docs/course-in-new-wizard-recon.md established the current
state (zero code exists for course-in-new-wizard) and the product fork
between compose-existing and split-and-author models.

## Strategic decisions locked

### Model: split-and-author

Course creation preserves the current behaviour — admin uploads one
document, AI splits it into sections, each section becomes a new
talk, all bundled into a course automatically. This matches how the
legacy wizard produces courses today and preserves the "AI does the
heavy lifting" value proposition central to the product's positioning.

The alternative (compose-existing — admin builds a course by picking
already-published talks and ordering them) was rejected. Faster to
build (~2 weeks vs ~4 weeks) via reuse of the orphaned CourseForm.tsx
component, but the trade-off — losing the "upload your manual, get a
course" flow — was judged unacceptable for the customer-value story.

### Track: additive parallel

The existing legacy-wizard course path stays live and untouched
throughout the build. The new-wizard course path is built alongside,
in new files, with its own controllers and frontend components.
Shared points are limited to the DB tables that both paths read/write
(Course, ToolboxTalkCourse, and adjacent).

This is deliberately conservative:
- No regression risk to existing course creation while the new one
  is built
- The trial customer keeps a working course path throughout the build
- If the new path hits a wall mid-build, the old path is fallback
- Cutover is a toggle flip, not a code merge

After cutover proves stable, the old course path becomes dead code —
same status as the legacy talk-creation wizard is about to be after
its own cutover. A subsequent cleanup pass deletes both. Cleanup and
cutover are kept as separate releases.

### Production status: hold until unified cutover

Everything shipped this week stays on transval until course-in-new-
wizard reaches parity. Reasons:
- Committing to the new wizard means committing to unified UX — a
  half-flipped state where standalone talks use the new wizard and
  courses use the legacy wizard creates the mixed-experience seam
  we explicitly rejected
- The trial customer keeps working infrastructure throughout —
  same wizard for both flows, no partial cutover to explain
- One unified release (course parity + toggle flip together) is
  cleaner than two partial ones

## Timeline

Realistic: 4-6 weeks from start of implementation to Production-ready.
- Optimistic (4 weeks) assumes smooth boss decisions on product
  questions and no significant mid-implementation surprises
- Realistic (5-6 weeks) accounts for typical scope expansion when a
  feature interacts with many other systems (assignment, scheduling,
  translation, external review)

To be re-estimated honestly one week into implementation once real
data on chunk sizing is available.

## Accumulated transval state (waiting for Production push)

The following shipped this week and is on transval, awaiting the
unified course-parity cutover:

- External review auto-apply refactor (Chunks 1-3)
- Refresh flow fixes: Weekly frequency guard, refresher creation-
  time notification, employee scoping on reminder queries
- Item #1 (new-wizard slideshow): Fix 0 + Shape D
- Item #6 (operator review mode for completed learnings)
- Item #4 (external review per-section-editable): Chunks A + B + C
  + D + E + F + timestamp/gate hygiene fixes

Plus BACKLOG entries added during recon:
- ProcessToolboxTalkScheduleCommandHandler send-before-save issue
- Employee.IsActive / User.IsActive sync gap on edit-form path

## Design decisions still pending

The following require the boss's input during implementation, not
upfront:

- Course creation UX in the new wizard: does course creation feel
  like standalone-talk creation with one extra step, or is it a
  distinct wizard variant, or something else?
- Course-level translation: reviewer sees one language across all
  items, or one item across languages?
- Course-level external review: per-item or course-wide? Given item
  #4 shipped per-section external review at the talk level, this
  becomes an interesting nesting question
- Course-level validation: per-item, course-wide, or both?
- Course-level scheduling: how do course refreshers interact with
  per-item refreshers?

These are enumerated fully in the first course-in-new-wizard recon
(coming next).

## Track 1 implementation approach — key principles

- New files for new code; do not modify legacy-course files
- Shared entities (Course, ToolboxTalkCourse) are read/written by
  both paths — treat their schema as stable during the build
- Any new field required by the new path must be nullable (backward
  compat with legacy path's existing rows)
- Legacy course tests stay green throughout; new-path tests are
  additive
- Every implementation chunk includes recon of legacy-path behaviour
  for the specific concern being implemented, so the new path
  matches semantics where semantics matter and diverges deliberately
  where they don't

## Next step

First recon on course-in-new-wizard implementation shape.
Deliverable: enumeration of product questions the boss needs to
answer, characterisation of the first implementation chunk, and
estimate of how many chunks the full build requires.