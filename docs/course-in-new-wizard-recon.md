# Course-in-New-Wizard Recon Report

**Date:** 2026-07-09
**Scope:** Read-only investigation. No code changes.
**Purpose:** Establish the current state, target state, and honest scope of moving course creation from the legacy wizard into the new wizard, ahead of retiring the legacy wizard as a supported creation path.

---

## Headline

**Ambiguous between Scenario B and Scenario C — no design doc and zero course-creation code exist in the new wizard (pure Scenario C by the letter), but a substantial, fully-working, currently-orphaned "compose existing talks into a course" API/UI already exists elsewhere in the codebase (`CourseForm.tsx` + Course CRUD API) that could pull this toward Scenario B-level effort *if* the boss picks that model over replicating the legacy "split one document into course sections" behavior. The single biggest scope driver is a product decision (Part 4, Q1), not a technical unknown.**

Also, correcting a likely session-level misattribution: **§24 does not refer to course-in-new-wizard support.** §24 is "Edit workflow for new-wizard talks" — a fully-shipped (2026-06-18), fully-closed backlog item about editing *published standalone Toolbox Talks* post-publish. It gates the `UseNewWizard` toggle flip for talks, not courses. See Part 1.3 for the full trace.

---

## Part 1 — Current state

### 1.1 Legacy course creation flow

There are **two separate mechanisms** in the codebase for "courses," and creation only exposes one of them — this split is itself important context for the scope estimate.

**Mechanism A — dedicated compose-existing-talks form (edit-only in practice, fully built):**
`CourseForm.tsx` (`web/src/features/toolbox-talks/components/CourseForm.tsx`) is a real react-hook-form + zod form: Title, Description, Active toggle, sequential-completion toggle, certificate toggle, refresher settings, auto-assign settings, and an "Learnings" section using `@dnd-kit` to add/reorder/remove course items via `AddTalksDialog` (which lists **already-published** talks only). It calls a full Course CRUD API (`ToolboxTalkCoursesController.cs`: `POST/PUT/GET/DELETE /api/toolbox-talks/courses`, `POST/PUT/DELETE .../{id}/items`). `CreateToolboxTalkCourseCommandHandler.cs` explicitly validates every referenced talk ID already exists and throws if not — **it never authors new talk content**.

This form is only reachable via `.../courses/[id]/edit/page.tsx` (editing an existing course). There is no creation route for it — `.../courses/new/page.tsx` is a hard redirect to the legacy wizard, and the course list's "Create New" button also routes straight there (`CourseList.tsx:256-261`).

**Mechanism B — legacy wizard's "Course" Output Type (the only reachable creation path today):**
Inside the legacy `/admin/toolbox-talks/create` SPA wizard, step 2 (`OutputTypeSelector.tsx`) offers "Single Lesson" vs. "Course" ("Each section becomes a separate lesson in an ordered course"). Picking Course treats the **entire uploaded document as one draft talk** through Parse/Quiz/Settings/Translate/Validate. Only at **Publish** does `ContentCreationSessionService.PublishAsCourseAsync` (~400 lines, `ContentCreationSessionService.cs:1779-2174`) retroactively:
- Creates a new `ToolboxTalkCourse`.
- Creates **one brand-new `ToolboxTalk`** per parsed section (each `IsPartOfCourse = true`) and adds each as a `ToolboxTalkCourseItem`.
- For Video input mode, repurposes the original draft talk as a "Full Video" course item instead of deleting it.
- Migrates the draft's per-section translation JSON into fresh `ToolboxTalkTranslation` rows per new talk.
- **Reassociates** (not re-runs) the existing `TranslationValidationRun`: `run.CourseId = course.Id; run.ToolboxTalkId = null;`.

So today, "course creation" means: upload one document → AI sections it → each section becomes its own new talk → bundle into a course. It is fundamentally an *authoring* model, not a *composing* model — the opposite mental model from Mechanism A, which is fully built but orphaned from creation.

**Data model** (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/`):
- `ToolboxTalkCourse` (TenantEntity) — title/description/active + sequential-completion, refresher, certificate, auto-assign settings.
- `ToolboxTalkCourseItem` (BaseEntity, join) — `CourseId`, `ToolboxTalkId`, `OrderIndex`, `IsRequired`; a talk can appear in multiple courses.
- `ToolboxTalkCourseTranslation` (BaseEntity) — course-level title/description translation only, per language.
- `ToolboxTalkCourseAssignment` (TenantEntity) — employee assignment with progress/refresher-chain fields; fans out into normal `ScheduledTalk` rows tagged with `CourseAssignmentId`/`CourseOrderIndex` at assignment time (`AssignCourseCommandHandler.cs:94-153`) — this **bypasses** `ToolboxTalkSchedule`/`ToolboxTalkScheduleAssignment` entirely, it's a separate one-shot fan-out, not built on the per-talk scheduling engine.
- `ToolboxTalkCertificate.CertificateType.Course` — a distinct certificate type/numbering series, generated by `CourseProgressService`/`CertificateGenerationService` when all required course items complete.

### 1.2 New wizard structure

Route tree under `web/src/app/(authenticated)/admin/toolbox-talks/learnings/`:

```
learnings/
├── new/page.tsx                → Step 1: Input & Config
└── [talkId]/
    ├── parse/page.tsx          → Step 2: Parse
    ├── quiz/page.tsx           → Step 3: Quiz
    ├── settings/page.tsx       → Step 4: Settings
    ├── translate/page.tsx      → Step 5: Translate
    ├── validate/page.tsx       → Step 6: Validate
    └── publish/page.tsx        → Step 7: Publish
```

Canonical step list lives in `stepOrder.ts:16-24`. `InputMode` (Text/Pdf/Video/Docx) is strictly single-file — every handler takes one `File`, indexes `[0]`, and `InitialiseToolboxTalkCommandHandler` sets fields on exactly one new `ToolboxTalk`. **No batch/multi-file path exists anywhere.**

The new wizard's Publish step (`PublishToolboxTalkCommandHandler.cs`) does nothing more than flip an existing `ToolboxTalk.Status` to `Published` — no `ToolboxTalkCourse`, no `ContentCreationSession`, no `OutputType` branching anywhere in this path.

**Course references in the new wizard's code: zero.** Exhaustive grep across `web/src/app/.../learnings/**` and `web/src/features/toolbox-talks/components/learning-wizard/**` for "course" (case-insensitive) returns exactly one hit — a `isPartOfCourse: false` mock value in a test file's `ToolboxTalk` type fixture, unrelated to any course feature. No hooks, components, routes, types, or commented-out code suggest course support was ever started in the new wizard. All course-aware backend code (`ContentCreationSession.OutputType`/`OutputCourseId`, `PublishAsCourseAsync`) belongs exclusively to the **legacy** wizard's `ContentCreationController`/`ContentCreationSessionService` — the new wizard's controller actions (`initialise`, `{id}/parse`, `{id}/quiz/*`, `{id}/settings`, `{id}/publish`) never reference any of it.

### 1.3 §24 — what it actually is (correction)

**§24 is "Edit workflow for new-wizard talks" — nothing to do with courses.** Verbatim from `BACKLOG.md:2039`:

> `## 24. Edit workflow for new-wizard talks (P0 — design specified, implementation chunks to scope)`
> Design rules locked 2026-06-15. Six chunks (talk detail edit UI, translation re-run UI, validate step UI, quiz editing UI, add-language, stale-translation gate). **"All 6 chunks shipped 2026-06-18. Demo cut and toggle-flip cut both engineering-complete. §24 fully closed."**

`docs/24/chunk-sizing-recon.md:331,377` is explicit that courses are outside §24's scope: *"The 6-step creation wizard remains untouched throughout §24"* and *"Course-level validation stays on the course pages. Not changed by §24."* Every other reference to "§24" in the repo (~50 hits across `BACKLOG.md`, `CLAUDE.md`, `docs/24/*`, `docs/phase-5/*`, code comments) is consistent with this meaning — there is no alternate §24.

**Conclusion:** the session-level phrasing "§24 referenced as engineering-complete but not toggled" is accurate for its actual subject (the Edit workflow gating the talk-creation toggle flip) but was very likely misapplied to "course support" in compressed session context. Course-in-new-wizard has no backlog number, no design doc, and no shipped chunks — it is simply a gap that hasn't been scoped yet.

---

## Part 2 — Existing artifacts

No design doc, recon, or implementation plan for "course in new wizard" exists anywhere in the repo. Checked:
- `docs/` full listing (49 files + `24/`, `25/`, `26/`, `phase-5/`, `playwright/` subdirectories) — no filename references course creation or a new-wizard course flow.
- `Glob **/designs/**` and `Glob **/planning/**` — no matches anywhere in the repo.
- The most recent and most relevant adjacent doc is **`docs/wizard-toggle-retirement-recon.md` (dated 2026-07-09 — today)**, which is about retiring the toggle UI, not building course support. It states plainly: *"Courses are permanently excluded from the toggle... because no new-wizard course-creation flow exists. This was true at §5.27 and remains true today."* — i.e. it documents the gap as a known, current constraint rather than scoping a fix.
- BACKLOG §7.3 (per-section external review escalation) and §3.17 (course assignment supervisor-scoping) touch courses tangentially but are unrelated features, not course-in-new-wizard scoping.

**Nothing was ever started.** There is no half-finished feature flag, no unused hook, no dead API route, no TODO in the new wizard's code mentioning courses (confirmed by the exhaustive grep in Part 1.2). This is a genuinely blank slate for the *new wizard specifically* — the only "started" material is Mechanism A (`CourseForm.tsx` + Course CRUD API), which predates the new wizard entirely and was built for the edit flow, not as a stepping stone toward new-wizard course creation.

---

## Part 3 — Feature parity assessment

| Feature | New wizard (talks) | Legacy courses | Status |
|---|---|---|---|
| Course-level metadata (title/desc/settings) | N/A | Yes — `ToolboxTalkCourse` + `CourseForm.tsx` | **Handled naturally** — reuse existing entity/API as-is |
| Course item sequencing/ordering | N/A | Yes — `@dnd-kit` drag-reorder in `CourseForm.tsx`, `ToolboxTalkCourseItem.OrderIndex` | **Handled naturally** — proven UI pattern to lift into a wizard step |
| Course-level completion (all-required-items) | N/A | Yes — `CourseProgressService`, fully wired | **Handled naturally** — no changes needed regardless of authoring wizard |
| Course-level scheduling | N/A | Yes, but bypasses `ToolboxTalkSchedule` entirely — a separate one-shot fan-out into `ScheduledTalk` rows (`AssignCourseCommandHandler.cs:94-153`) | **Handled naturally for MVP** (assignment happens post-creation via the existing edit page, same as today) |
| Course-level operator assignment | N/A | Yes — `AssignCourseCommand`, `AssignCourseDialog` | **Handled naturally** — reuse as-is |
| Course-level translation | N/A (new wizard has no course concept) | Course-level: title/description only (`ToolboxTalkCourseTranslation`). Item-level: each course-item talk carries its own full `ToolboxTalkTranslation` | **Small adaptation** — if the "compose existing talks" model is chosen, items are already-validated published talks, so no new course-level translation work is needed beyond title/description (already-built pattern). If the "split one document" model is chosen, this requires porting `PublishAsCourseAsync`'s translation-migration logic (~70 lines) into the new architecture. |
| Course-level validation | N/A | `TranslationValidationRun.CourseId` (nullable FK) is **already a first-class, fully-wired concept** — `TranslationValidationController.cs` has course-scoped endpoints, admin UI exists at `/admin/toolbox-talks/courses/[id]/validation/[runId]` | **Substantial reuse available** — this is the strongest existing seam. Reachable only via legacy `PublishAsCourseAsync` today, but the schema/API/UI already support course-scoped validation; a new-wizard equivalent would attach to the existing shape rather than invent one. Not needed at all if "compose existing" model is chosen (items are pre-validated). |
| Course-level external review | N/A | **Does not exist for either.** `ExternalParticipantInvitation.TargetEntityId` is always a talk ID — no course concept anywhere in the workflow-review entities | **Requires product decision** — not built, not explicitly de-scoped as "never," but no design discussion of a course variant exists anywhere in BACKLOG/docs |
| Certificate at course completion | N/A | Yes — distinct `CertificateType.Course`, separate numbering series, fully wired in `CertificateGenerationService` | **Handled naturally** — no changes needed |
| Employee-facing course experience | — | Wizard-agnostic — `courses/[id]/page.tsx` reads only `ToolboxTalkCourseAssignment` + `ScheduledTalks`, with zero dependency on which UI authored the course | **Handled naturally** — this is good news: once course rows exist, the entire employee experience (including the just-shipped "review mode," which is a `ScheduledTalk`-level feature) works unmodified regardless of which wizard created them |
| Slideshow generation | Yes (per-talk) | N/A — never course-scoped in either wizard; `ToolboxTalkCourse` has zero slideshow fields | **Not a gap** — structurally a per-talk-only feature in both flows, so course support doesn't need to solve this |

**Net read:** the *consumption* side of courses (employee experience, completion, certificates, assignment) is entirely wizard-agnostic and needs zero new work. The *authoring* side is where all the gap is — and its size depends entirely on which of the two existing mental models (compose vs. split-and-author) the new wizard is meant to replicate.

---

## Part 4 — Product questions

Ranked by whether they gate implementation start.

### Gates implementation (must be answered first)

1. **Which course model does the new wizard implement — "compose existing already-published talks" (Mechanism A, already built as `CourseForm.tsx`/Course CRUD API but orphaned from creation) or "split one uploaded document into course sections, authoring new talks" (Mechanism B, the legacy wizard's actual behavior today)?** This is the single largest scope driver. Choosing Mechanism A could mean lifting an existing, proven form into a new wizard step (small); choosing Mechanism B means porting ~400 lines of `PublishAsCourseAsync` logic (section-splitting, per-item talk creation, translation migration, validation-run reassociation) into the new wizard's URL-per-step, server-side-state architecture (large — essentially a second `ContentCreationSession`-equivalent for the new wizard).

2. **Does a course-in-new-wizard use its own step sequence, or reuse the existing 7-step flow N times (once per selected talk) plus a wrapping sequencing step?** Cascades directly from Q1 — a "compose existing" model needs a lightweight new step (pick talks + reorder + course metadata), not a new 7-step run per item; a "split document" model needs the entire existing flow retained for the single upload, with course-splitting only surfacing at Publish, mirroring the legacy shape.

3. **Is per-course validation needed at all, or does the compose model rely purely on each item's pre-existing validation?** If Mechanism A is chosen, course items are already-published, already-validated talks — no new validation work is needed and `TranslationValidationRun.CourseId` may go entirely unused by the new path. If Mechanism B is chosen, the reassociation logic from `PublishAsCourseAsync` needs an equivalent in the new architecture.

### Can be resolved during implementation

4. **External review at course level — per-item or course-wide?** Since external review has no course concept today for either wizard, and legacy courses ship without it, this can reasonably be deferred/punted for an MVP course-in-new-wizard cut without blocking the rest of the work.

5. **Do courses in the new wizard need parity with this week's shipped features (slideshow, per-section external review, review mode)?** Slideshow is structurally not course-scoped in either flow (non-issue). Review mode is inherited for free via `ScheduledTalk` regardless of authoring wizard (non-issue). Per-section external review only matters if Q4 resolves toward "yes, course items need external review" — otherwise moot.

6. **How do course items get sequenced in the new wizard — ordered list (existing `@dnd-kit` pattern from `CourseForm.tsx`) vs. something more elaborate?** Low-risk either way; the existing pattern is proven and would very likely just be reused regardless of which model is picked, so this doesn't block starting Chunk 1.

---

## Part 5 — Scope estimate

**Scenario: Ambiguous between B and C.** By the strict letter of "does course-in-new-wizard code/design exist" the answer is no — that's Scenario C (little to nothing exists, full design + build). But treating this as a from-scratch build would ignore that the *hard parts* of a course feature (metadata form, sequencing UI, assignment, completion, certificates, and even a course-scoped validation-run schema) are already fully built and working, just serving a different creation path. If the boss picks Mechanism A (compose existing talks) as the new wizard's course model, the actual new-wizard-specific work shrinks to "a course-creation entry point + a talk-picker/sequencer step wired into the wizard's step framework, calling the already-existing Course CRUD API" — which is Scenario B territory (some code exists, design mostly falls out of Mechanism A's existing shape, few boss decisions needed). If the boss wants Mechanism B replicated (split-one-document authoring, matching legacy behavior exactly), it's a much larger port of `ContentCreationSessionService`'s course-publish logic into new-wizard architecture — solidly Scenario C.

Given the ambiguity is itself the answer: **do not scope implementation chunks until Q1 (Part 4) is answered** — the two branches differ by roughly an order of magnitude in effort.

If pressed for a single planning number pending that decision: **Scenario B, ~1.5–2.5 weeks over 3-4 chunks**, assuming Mechanism A is chosen (this is also the option that better matches "unify the wizard experience" as a genuine simplification rather than a re-implementation, and avoids permanently maintaining two divergent course-authoring code paths). If Mechanism B is chosen instead, treat this as **Scenario C, 3-4 weeks including upfront design**, since it requires reproducing bespoke section-splitting/authoring logic inside a fundamentally different (server-side, URL-per-step) wizard architecture.

### First three chunks (assuming Q1 resolves to Mechanism A — compose existing talks)

1. **Course entry point + talk-picker/sequencer step.** New route under `learnings/` (e.g. `learnings/course/new`) using the existing `stepOrder.ts` step-shell pattern; reuse `AddTalksDialog` + `@dnd-kit` reordering from `CourseForm.tsx`; wire to existing `POST /api/toolbox-talks/courses` + `POST .../items` endpoints. No new backend command needed — this is largely frontend composition of already-built pieces.
2. **Course metadata + settings step + Publish.** Title/description/sequential-completion/certificate/refresher/auto-assign fields (already modeled on `ToolboxTalkCourse`), publish action redirecting to the course detail page. Confirm `CourseList.tsx`/`courses/new/page.tsx` now route here instead of the legacy wizard once `UseNewWizard` is true.
3. **Regression + parity pass.** Confirm employee-facing course experience, assignment, completion, and certificate generation are unaffected (expected to be true unmodified, per Part 3's "wizard-agnostic" finding — this chunk is verification, not new code). Update `docs/wizard-toggle-retirement-recon.md`'s "Courses are permanently excluded from the toggle" finding, since it would no longer be true. Decide (Q4) whether external review is explicitly out of scope for this cut or deferred to a follow-up chunk.

(If Q1 instead resolves to Mechanism B, the first three chunks would instead be: design the new-wizard-equivalent of a course-authoring session state → port section-splitting/per-item-talk-creation logic → port translation-migration + validation-run reassociation logic — a materially different and larger set of chunks not detailed further here pending that decision.)

---

## Part 6 — Rough time-to-Production

Assuming Mechanism A is chosen (the scenario this estimate leans toward):

- **Optimistic:** ~7-10 days — if the boss answers Q1-Q3 same-day, the compose-model reuse holds up exactly as it looks on paper, and no surprises emerge in wiring the wizard step framework to the existing Course API.
- **Realistic:** ~2-3 weeks — accounting for typical decision turnaround and at least one mid-implementation surprise (e.g., the wizard step framework's URL-per-step/server-side-state model not mapping as cleanly onto "pick N existing talks" as it does onto "process one uploaded file," given every existing step assumes a single in-progress `ToolboxTalk`, not a course).
- **Pessimistic:** ~4-5 weeks — if Q1 is decided in favor of Mechanism B after implementation has already started under a Mechanism A assumption (a "wrong turn" scenario), or if course-level external review (Q4) turns out to be a hard requirement discovered late, forcing new workflow-review entity design.

If Mechanism B is chosen from the outset, shift all three estimates up by roughly 2-3 weeks (3-4 weeks / 5-6 weeks / 7-8 weeks) to account for the larger port of `ContentCreationSessionService`'s course logic into the new architecture.

---

## Notes for the boss

Course creation has no presence at all in the new wizard today — zero code, no design doc, nothing parked or half-built specifically for it (separately: §24, which a compressed session note attributed to course support, is actually the unrelated and already-fully-shipped "editing published talks" work, so that reference should be disregarded for this decision). The one thing worth knowing before scoping this: the codebase already has two different, fully-working ideas of what a "course" is — the legacy wizard authors a course by splitting one uploaded document into several brand-new talks, while a separate, currently creation-inaccessible form (`CourseForm.tsx`) composes a course out of talks that already exist and are already published. Picking the second model as the new wizard's course-creation approach would let this reuse a proven UI and a fully-built API almost as-is (roughly 2 weeks), while replicating the first model would mean porting a large, bespoke section-splitting pipeline into the new wizard's different architecture (closer to a month). Recommend prioritizing that one decision — compose-existing vs. split-and-author — since every other open question and every chunk estimate cascades from it.
