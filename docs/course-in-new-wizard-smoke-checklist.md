# Course-in-new-wizard — manual smoke checklist (Development)

## Purpose

Manual walk-through checklist for the compose-existing course path
shipped across Chunks 1-4. Companion to the automated integration
test `Course_ComposeExistingHappyPath_CreateAddPublishAssignVerify`
(`tests/QuantumBuild.Tests.Integration/ToolboxTalks/CourseComposeExistingHappyPathTests.cs`),
which locks the backend happy path against regression. This checklist
covers the frontend/UI behaviour the automated test does not reach.

Run against the Development environment (or local `npm run dev` +
`dotnet run` against a Development-seeded database). Mark each item
pass/fail as you go; file a bug for any failure instead of fixing
inline.

## a. Toggle-on default path (`UseNewCourseCreation` = true, default)

- [ ] On the Courses landing page (`/admin/toolbox-talks/courses`),
      click **Create New**. Expected: the compose-existing `CourseForm`
      renders directly — no redirect to the legacy wizard.
- [ ] The form's available-talks list shows **Published talks only**.
      Expected: no Draft talks appear in the picker, even if Draft
      talks exist in the tenant.
- [ ] Select 2-3 talks from the list. Expected: selected talks move
      into an ordered "selected" list/panel.
- [ ] Drag-reorder the selected talks. Expected: the order updates
      visibly and persists in the form state (re-check order after
      reordering, before submitting).
- [ ] Fill in course metadata (title, description, refresher/
      certificate/auto-assign options as applicable). Expected: all
      fields accept input and validate as expected (e.g. title
      required).
- [ ] Submit / publish the course. Expected: success toast, no
      console errors.
- [ ] Navigate back to the Courses list. Expected: the newly created
      course appears with the correct title and talk count.

## b. Toggle-off path (`?coursemode=old` URL override)

- [ ] From the Courses landing page, append `?coursemode=old` to the
      URL and click **Create New** (or navigate directly to the
      Create New URL with the query param). Expected: redirects to
      the legacy course wizard, not the compose-existing form.
- [ ] Navigate away and click **Create New** again **without** the
      query param. Expected: back to the compose-existing form — the
      override is one-shot and does not persist to localStorage/
      cookie (per Note 29 in CLAUDE.md).

## c. Draft-talk guard (backend + frontend)

- [ ] In the compose-existing form's talk picker, confirm no Draft
      talks are selectable (covered above in section a, re-verify
      here in isolation with a tenant that has at least one Draft
      talk).
- [ ] Using an API tool (e.g. Postman/curl) or direct URL
      manipulation, attempt to `POST /api/toolbox-talks/courses` with
      a Draft talk's ID included in `Items`. Expected: `400 Bad
      Request` with a message mentioning "published" (matches
      automated coverage in `CourseCompositionTests.cs`).
- [ ] Attempt the equivalent against `PUT /api/toolbox-talks/courses/
      {id}/items` (add a Draft talk to an existing published course).
      Expected: same `400 Bad Request` guard.

## d. Interaction with review-mode

- [ ] As an Operator with the composed course assigned, go to **My
      Learnings** and open the course.
- [ ] Complete the first course item (talk). Expected: standard
      completion flow (video/sections/quiz/signature as configured).
- [ ] After completion, return to the course/talk view. Expected: a
      review-mode link/button is visible on the completed item,
      consistent with existing review-mode behaviour for
      non-course-assigned talks.
- [ ] If `RequireSequentialCompletion` is enabled on the course,
      confirm the next item unlocks only after the previous item is
      completed.

## e. Interaction with external review

- [ ] No interaction expected. External review is per-talk, not
      per-course, and the compose-existing flow does not touch it.
      Not smoke-tested here — noted for completeness only.

## Sign-off

- [ ] All sections above completed with no unresolved failures, or
      failures filed as separate bugs/backlog items.
- Tester: ______________________  Date: ______________________
