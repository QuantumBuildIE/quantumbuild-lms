# Operator Access to Previous Learnings — Recon (Boss Item #6)

**Date:** 2026-07-08
**Scope:** Read-only recon. No code changed.

## 1. Headline

**Partial — operator sees past learnings but no re-take affordance / partial re-take support**

Operators can see every previously-assigned, non-cancelled learning (including completed ones) through the "My Learnings" tabs, and can open a completed talk's detail record. But there is no code path — backend or frontend — that lets an operator start a fresh attempt on a talk they've already completed. The completion command explicitly rejects it, and the completed-talk detail screen doesn't even show a "review the content again" option; it shows a certificate/summary card only.

---

## 2. Part 1 findings — what the operator sees today

### Endpoints (`MyToolboxTalksController.cs`, `src/QuantumBuild.API/Controllers/MyToolboxTalksController.cs`)

| Endpoint | Controller lines | Handler | Filter logic |
|---|---|---|---|
| `GET /api/my/toolbox-talks` (all) | 67–99 | `GetMyToolboxTalksQueryHandler` | No status param passed |
| `GET /api/my/toolbox-talks/pending` | 402–433 | same handler, `Status=Pending` | |
| `GET /api/my/toolbox-talks/in-progress` | 439–470 | same, `Status=InProgress` | |
| `GET /api/my/toolbox-talks/overdue` | 476–507 | same, `Status=Overdue` | |
| `GET /api/my/toolbox-talks/completed` | 513–544 | same, `Status=Completed` | |
| `GET /api/my/toolbox-talks/{id}` | 106–139 | `GetMyToolboxTalkByIdQueryHandler` | By `ScheduledTalkId`, any status |
| `GET /api/my/toolbox-talks/summary` | 550–607 | 3x calls to the list handler (Pending/InProgress/Overdue counts only) | |
| `POST .../start`, `.../sections/{id}/read`, `.../quiz/submit`, `.../video-progress`, `.../reset-video-progress`, `.../complete` | 148–396 | respective commands | |
| `GET .../subtitles/status`, `.../subtitles/{lang}`, `.../slides`, `.../slideshow` | 614–834 | verify assignment via `GetMyToolboxTalkByIdQueryHandler` first | |
| `GET .../courses`, `.../courses/{id}` | 839–903 | `GetEmployeeCourseAssignmentsQuery` / `GetMyCourseAssignmentByIdQuery` | |
| `GET .../certificates`, `.../certificates/{id}/download` | 909–983 | `GetMyCertificatesQuery` / `GetCertificateDownloadQuery` | |

**List query — `GetMyToolboxTalksQueryHandler.cs:23–28`:**

```csharp
var query = _context.ScheduledTalks
    .Where(st => st.TenantId == request.TenantId &&
                st.EmployeeId == request.EmployeeId &&
                !st.IsDeleted &&
                st.Status != ScheduledTalkStatus.Cancelled)
```

- Base filter excludes only `IsDeleted` rows and `Cancelled` status. Every other status (Pending, InProgress, Overdue, Completed) is included when no status filter is applied — this is what the "All" tab shows.
- Optional `request.Status` filter (line 31–34) narrows to one status for the dedicated tabs.
- Ordering (line 41–42): incomplete talks first, then by due date; completed talks sort last within their group.

**Detail query — `GetMyToolboxTalkByIdQueryHandler.cs:29–44`:** has **no status restriction at all** — only `Id`, `TenantId`, `EmployeeId`, `!IsDeleted`. This means:
- A completed talk's full detail (sections, quiz questions, translations, completion record) is fetchable by ID (line 118–159 builds the DTO, including `CompletedAt`/`CertificateUrl` at 155–156).
- A **Cancelled** talk is excluded from every list tab (per the list handler's filter) but is **not** blocked at the detail-query level — if an operator has/guesses the `scheduledTalkId`, `GET /{id}` still returns it. This is a minor inconsistency, not currently reachable via any UI button (nothing links to a cancelled talk's ID), but worth noting as a loophole.

### Frontend

- `web/src/app/(authenticated)/toolbox-talks/page.tsx` — renders `MyCoursesList` + `MyTalksList`.
- `web/src/features/toolbox-talks/components/MyTalksList.tsx` — five tabs: Pending, In Progress, Overdue, Completed, All (`tabConfigs`, lines 30–61). Each tab is backed by its own hook/query (lines 155–159). Clicking a card routes to `/toolbox-talks/{scheduledTalkId}` (`handleAction`, lines 161–163).
- `web/src/app/(authenticated)/toolbox-talks/[id]/page.tsx` → renders `TalkViewer` (`web/src/features/toolbox-talks/components/TalkViewer.tsx`).
- **What happens when an operator opens a Completed talk:** `TalkViewer.tsx:249–252` — on first load, if `talk.status === 'Completed' && talk.completedAt`, the component immediately sets `currentStep = 'complete'` and never renders the video/sections/quiz UI. The render branch at `TalkViewer.tsx:440–474` then shows only `<CompletionSuccess>`.
- `CompletionSuccess.tsx` (entire file) shows: a success banner, a stats grid (completed date, time spent, video-watch %, quiz score), the signature confirmation, a **certificate download button** (lines 195–214, only rendered `if (completion.certificateUrl)`), and a "Back to My Learnings" button (217–224). **There is no button, link, or code path anywhere in this component (or `TalkViewer`) that re-opens the video/sections/quiz content or starts a new attempt.**

**Conclusion for Part 1:** operators today can (a) see a list of every prior assignment across Pending/InProgress/Overdue/Completed via tabs, and (b) open a completed talk to see a read-only summary + certificate download. They cannot review the original section content, video, or quiz questions of a completed talk (no retrospective content view exists), and there is no re-take entry point in the UI.

---

## 3. Part 2 findings — the re-take question

### Does completion get blocked on a second attempt?

**Yes — three explicit guards, `CompleteToolboxTalkCommandHandler.cs`:**

```csharp
// line 83-86
if (scheduledTalk.Status == ScheduledTalkStatus.Completed)
    throw new InvalidOperationException("This scheduled talk has already been completed.");

// line 88-91
if (scheduledTalk.Status == ScheduledTalkStatus.Cancelled)
    throw new InvalidOperationException("This scheduled talk has been cancelled.");

// line 93-96
if (scheduledTalk.Completion != null)
    throw new InvalidOperationException("A completion record already exists for this scheduled talk.");
```

Any of the three independently blocks re-completion of the same `ScheduledTalk` row. There is no upsert/overwrite path — `ScheduledTalkCompletions.Add(completion)` (line 192) is a straight insert, and the guards above prevent it from ever running twice for the same row.

**`StartToolboxTalkCommandHandler.cs:61–65`** — starting a Completed or Cancelled talk is a silent no-op (`return Unit.Value`), not an error. This matters because `TalkViewer.tsx` calls `/start` automatically on load (lines 275–300) but guards `if (talk.status === 'Completed' ...) return;` (line 277) client-side too, so in practice `/start` is never even called for a completed talk from the current UI.

### Certificates on re-completion

Not reachable — since completion is blocked outright, there is no code path where a second `ScheduledTalkCertificate`/completion record could be created for the same `ScheduledTalk`. Certificate generation (`CompleteToolboxTalkCommandHandler.cs:210–240`) only runs once, inside the single allowed completion flow.

### Existing "retake"-adjacent mechanisms — none found

Case-insensitive grep for `retake`, `re-take`, `recomplete`, `re-complete`, `reset` across both `src/` and `web/src/`:
- **Zero** hits for `retake` / `re-take` / `recomplete` in either tree.
- `reset` hits are all unrelated: password reset (`ResetPasswordDto.cs`, `reset-password-dialog.tsx`, `change-password-form.tsx`), Hangfire tenant-data reset (`ResetTenantDataCommand(Handler).cs`, admin tenants page), and **`ResetVideoProgress`**.

`ResetVideoProgress` is a red herring for this question: `ResetVideoProgressCommandHandler.cs:60–63` explicitly blocks it once `Status == Completed` (same guard pattern as completion). Its only caller is `TalkViewer.tsx:363–371` (`handleRewatchVideo`), wired to the "rewatch video" action inside the **in-progress quiz retry flow** (`QuizSection` component's `onRewatchVideo` prop, `TalkViewer.tsx:644`) — i.e., an employee who fails the quiz on their current, not-yet-completed attempt can rewatch the video and retry the quiz. It has nothing to do with revisiting a talk that's already in `Completed` status.

**Conclusion for Part 2: no retake mechanism exists today, and it is not reachable end-to-end (backend explicitly rejects it; frontend has no button that would call it).**

### Plausible minimal shapes (described only, not designed/built)

1. **Self-service re-schedule endpoint** — a new `POST /api/my/toolbox-talks/{id}/retake` that clones the completed `ScheduledTalk` into a new `Pending` row (same `ToolboxTalkId`, same `EmployeeId`, new `Id`, `IsRefresher=false`), leaving the original completion record untouched for audit/compliance history. Closest to "re-take without being scheduled specifically."
2. **Blur view+retake into one flow** — change `TalkViewer.tsx` so a completed talk's detail page offers a "Practice Again" button that re-enters the video/sections/quiz UI in a non-graded, non-persisted mode (no new `ScheduledTalkCompletion`, no certificate implications). Lowest compliance risk, but doesn't produce any record that the operator refreshed their knowledge.
3. **Separate non-assignment "review session" entity** — a lightweight `TalkReviewSession` (or similar) that lets an operator open any of their historically-completed talks' original content read-only or re-take the quiz for self-check, independent of `ScheduledTalk`/`ScheduledTalkCompletion`, with no compliance-reporting side effects at all.

---

## 4. Part 3 — characterization against the boss's intent

**Resolving the "assigned" ambiguity:** the boss's phrasing conflates two things — "all previous learnings that were assigned to them" (broad) and "re-take them... only ones that were previously assigned" (implies talks they've already been through, i.e., completed). The code has two distinct notions:
- **"Ever had a ScheduledTalk row" (any non-cancelled status)** — this is what the **All** tab and the base `GetMyToolboxTalksQueryHandler` query return (`GetMyToolboxTalksQueryHandler.cs:23–28`). Pending/InProgress/Overdue/Completed are all visible here.
- **"Ever completed"** — the narrower `Completed`-status-only tab (`MyToolboxTalksController.cs:513–544`).

Because "re-take" only makes sense for something already finished, the operative population for the boss's request is the **Completed** set, not the broader "ever assigned" set. On that narrower reading:
- **View access:** Fully matches — completed talks are listed (Completed tab) and individually fetchable (`GET /{id}`), so "has access to all previous learnings... assigned to them" is true for the list/summary level.
- **Content review:** Gap — the completed-talk detail screen (`CompletionSuccess.tsx`) shows only a summary card + certificate download, not the original sections/video/quiz content. If "access to" implies being able to look at the material again, that part is not built.
- **Re-take (open access, unscheduled):** Gap — no mechanism exists at all, front or back end.

**Overall categorization: Partial.** Visibility of past learnings exists and works; re-take (the boss's actual emphasis) does not exist in any form.

---

## 5. Edge cases (traced from code, not speculated)

| Edge case | Answer | Evidence |
|---|---|---|
| Assigned, never completed, schedule later cancelled | Excluded from every operator tab (Pending/InProgress/Overdue/Completed/All) once its `ScheduledTalk.Status` flips to `Cancelled`. Still fetchable directly via `GET /{id}` if the operator has the URL/ID (detail query has no status filter). | `GetMyToolboxTalksQueryHandler.cs:27` (`st.Status != Cancelled`); `CancelToolboxTalkScheduleCommandHandler.cs:43–55` (schedule cancel → sets Pending/InProgress child rows to Cancelled); `GetMyToolboxTalkByIdQueryHandler.cs:29–44` (no status filter) |
| Underlying `ToolboxTalk` soft-deleted (`IsDeleted=true`) | Very likely disappears entirely from all operator tabs, **including Completed history**, because `ToolboxTalk` carries a global EF Core query filter (`!IsDeleted && tenant`) that EF applies through the required navigation used in the list/detail projections (`st.ToolboxTalk.Code`/`.Title` etc.). This is a documented, unresolved trap in this codebase — see CLAUDE.md Backlog "Low" item: *"Pre-existing technical warnings — Model.Validation[10622] query-filter warnings on required relationships."* Not independently re-verified with a live query in this recon (read-only, no DB access), flagged as high-confidence based on the model configuration, not empirically confirmed. | `ApplicationDbContext.cs:336` (`modelBuilder.Entity<ToolboxTalk>().HasQueryFilter(e => !e.IsDeleted && ...)`); CLAUDE.md Backlog "Low" section |
| Overdue or manually-Cancelled `ScheduledTalk` — visible anywhere? | **Overdue:** yes, has its own tab and appears in "All" (it's a normal, non-cancelled status). **Cancelled:** no, excluded from every tab; the admin-side cancel action (`DELETE /assigned/{id}`) explicitly refuses to cancel an already-Completed assignment, so a genuinely completed record can never retroactively become invisible this way. | `GetMyToolboxTalksQueryHandler.cs:27`; `CancelScheduledTalkCommandHandler.cs:34–38` |
| Video-mode vs PDF/slideshow-mode completed talk — does detail rendering branch correctly? | Moot — there is no retrospective content view for completed talks at all (see Part 1). `CompletionSuccess.tsx` renders identically regardless of `VideoSource`/slideshow presence; it only conditionally shows a "Video Watched %" stat (`CompletionSuccess.tsx:150–156`) if `completion.videoWatchPercent !== null`. No section/video/slideshow markup is rendered in the completed state either way. | `TalkViewer.tsx:440–474`; `CompletionSuccess.tsx` (whole file) |
| Employee soft-deleted and rejoined | Not deep-dived per task instructions. `Employee` carries the same global `!IsDeleted && tenant` query filter pattern (`ApplicationDbContext.cs:320`) as other tenant entities, and `GetMyToolboxTalkByIdQueryHandler.cs:39` `.Include(st => st.Employee)` pulls the employee record for `PreferredLanguage`. A soft-deleted employee likely can't authenticate at all (separate concern from this recon), so this scenario is probably moot in practice. Flagged only, not traced further. | `ApplicationDbContext.cs:320`; `GetMyToolboxTalkByIdQueryHandler.cs:39,51` |

**Distinct-from-retake point, confirmed:** certificate view/download for a completed talk works today regardless of any re-take capability — it's wired through `CompletionSuccess.tsx:195–214` (inline, tied to the completion record) and separately through the dedicated `/api/my/toolbox-talks/certificates` + `/certificates/{id}/download` endpoints (`MyToolboxTalksController.cs:909–983`) and the `/toolbox-talks/certificates` page. These are unaffected by whatever happens with the re-take question — an operator's certificate remains available whether or not a retake feature is ever built.

---

## 6. Recommended next step

Given the **Partial** verdict, this needs a **design conversation with the boss** before any implementation, because "re-take" touches compliance semantics that the current architecture doesn't have opinions on yet. Specific product questions to resolve first:

1. Does a re-take create a **new** `ScheduledTalkCertificate`/completion record, or does it overwrite/supersede the original? (Affects audit trail and "most recent completion" semantics used in reports.)
2. Does a re-take **count toward compliance reporting** (Skills Matrix, compliance report) the same way an admin-scheduled refresher does, or is it purely informal/self-directed and excluded from compliance stats?
3. Is there a **cooldown** (e.g., can't re-take the same talk more than once a day/week) to prevent gaming pass rates or generating certificate spam?
4. Does an admin need **visibility** into re-take activity (e.g., "Employee X retook Talk Y three times"), or is this entirely operator-private/informal?
5. Should re-take be available for **every** completed talk, or only certain ones (e.g., admin opts a talk into "allow self-review" via a new flag on `ToolboxTalk`)?
6. Is the boss's ask really "re-take" (full flow: video/sections/quiz/signature, produces a new completion record) or "review" (read-only look-back at the content, no new record) — the current wording says "re-take" but the smallest, lowest-risk version of this feature is "review."

Once those are answered, the smallest implementation chunk would likely be scoped around whichever of the three shapes in Part 2 best fits the answers — e.g., if the answer to Q1–Q3 leans toward "informal, no compliance impact," shape #2 (blur view+retake into a non-graded practice mode within `TalkViewer`) is the cheapest; if the answers lean toward "yes it should count and be tracked," shape #1 (self-schedule endpoint creating a real new `ScheduledTalk`) is more appropriate and should probably be scoped as its own chunk given it touches certificate generation, refresher scheduling, and reporting.

---

## 7. Notes for the boss

Right now, employees can already see everything that's ever been assigned to them — pending, in-progress, overdue, and completed — in their "My Learnings" tabs, and they can always re-download the certificate for anything they've finished. What's missing is the "re-take" part: once a learning is marked complete, there's no button anywhere that lets the employee go through it again — the system actively blocks a second completion, and the completed-talk screen only shows a summary and certificate, not the original content. So this is roughly half-built: the "see everything" half works today; the "open access to retake" half doesn't exist yet and would need a short design conversation (mainly: should a retake count for compliance, and should it create a new certificate?) before it's built, because that decision changes how big the work is.
