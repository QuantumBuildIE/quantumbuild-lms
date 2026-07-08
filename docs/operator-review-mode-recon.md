# Operator Review-Mode Recon (Boss Item #6, Shape 2 clean-check)

**Date:** 2026-07-08
**Scope:** Read-only recon. No code changed.

## 1. Headline

**Shape 2 is clean — 2 files change, backend untouched.**

`TalkViewer.tsx` and `CompletionSuccess.tsx` are the only files that need edits. No changes to `QuizSection.tsx`, `SectionContent.tsx`, `VideoPlayer.tsx`, `SignatureCapture.tsx`, or any backend controller/handler.

The reason this turns out to be clean is not that a mode flag is trivial in the abstract — it's that **the existing state machine already has a "previously-done" branch for every sub-step** (built originally to let an employee resume an in-progress talk without redoing finished work), and a completed talk satisfies every one of those "already done" conditions simultaneously:

- `SectionContent` never shows its acknowledgment checkbox or calls `onAcknowledge` once `section.isRead` is `true` at mount (`wasAlreadyRead.current` guard) — every section of a completed talk is already read, so this component is already inert/read-only.
- `QuizSection` renders its `alreadyPassed` short-circuit (a static "Quiz Passed! You scored X%" card with just a Continue button — no questions, no submit) whenever `lastQuizPassed === true`, which is guaranteed true for any completed talk that required a quiz.
- `StartToolboxTalkCommandHandler` already no-ops for `Completed` status, and `TalkViewer`'s mount effect already skips calling `/start` at all when `talk.status === 'Completed'`.

The only step in the whole flow with no "already done" branch is **Signature** — it always renders a live `SignatureCapture` wired to `handleComplete`, which would hit the backend's "already completed" guard. That is the one place review mode must actively intervene, by skipping the signature step's key from the available-steps list rather than by teaching `SignatureCapture` a new mode.

## 2. Part 1 — TalkViewer state machine analysis

### Completion side effects (mutations that write)

| Trigger | Hook | Backend write? | Guard against `Completed` status today |
|---|---|---|---|
| Mount (`TalkViewer.tsx:275-300`) | `useStartToolboxTalk` → `StartToolboxTalkCommandHandler` | Yes (`StartedLocationTimestamp`, status→InProgress) — but only on first-ever call | Frontend: effect body returns early if `talk.status === 'Completed'` (`TalkViewer.tsx:277`). Backend: handler also no-ops silently for Completed (`StartToolboxTalkCommandHandler.cs:61-65`). Double-safe. |
| Section acknowledge (`handleMarkSectionRead`, `TalkViewer.tsx:303-314`) | `useMarkSectionRead` → `MarkSectionReadCommandHandler` | Yes (`ScheduledTalkSectionProgress` insert/update, status→InProgress) | Backend **throws** `InvalidOperationException` if `Status == Completed` (`MarkSectionReadCommandHandler.cs:63-66`). Frontend never calls it for a completed talk today because `SectionContent` hides the checkbox once `section.isRead` (see below) — not because `TalkViewer` guards it. |
| Video progress tick (`handleVideoProgress`, `TalkViewer.tsx:316-325`) | `useUpdateVideoProgress` → `UpdateVideoProgressCommandHandler` | Yes (`VideoWatchPercent`, status→InProgress) | Backend **throws** if `Status == Completed` (`UpdateVideoProgressCommandHandler.cs:59-62`). Frontend has no guard — see risk below (§5, "video replay can still hit a throwing endpoint"). |
| Quiz submit (`handleQuizSubmit`, `TalkViewer.tsx:327-338`) | `useSubmitQuizAnswers` → `SubmitQuizAnswersCommandHandler` | Yes (new `ScheduledTalkQuizAttempt` row) | Backend **throws** if `Status == Completed` (`SubmitQuizAnswersCommandHandler.cs:66-69`). Frontend never calls it for a completed talk because `QuizSection`'s `alreadyPassed` branch never renders a submit button when `lastQuizPassed === true`. |
| Complete (`handleComplete`, `TalkViewer.tsx:340-361`) | `useCompleteToolboxTalk` → `CompleteToolboxTalkCommandHandler` | Yes (new `ScheduledTalkCompletion`, certificate, refresher) | Backend **throws** three ways (Status==Completed, Status==Cancelled, `Completion != null`) — see prior recon. Frontend has no guard; only reachable if the Signature step is rendered. |
| Rewatch video (`handleRewatchVideo`, `TalkViewer.tsx:363-371`) | `useResetVideoProgress` → `ResetVideoProgressCommandHandler` | Yes (`VideoWatchPercent = 0`) | Backend **throws** if `Status == Completed`. Only reachable via `QuizSection`'s `onRewatchVideo` prop, which is only passed into the live (not-yet-passed) quiz UI — unreachable once `alreadyPassed` is true. |

**Key correction to the design assumption:** four of these five write-mutations are not merely "unsafe to suppress" — they actively **throw a 4xx-mapped exception** if called against a `Completed` `ScheduledTalk`. None of that matters today because the existing UI branches (`section.isRead`, `alreadyPassed`, the mount-effect guard) already prevent them from ever being called for a completed talk. Review mode inherits this protection for free on three of the four; only video-progress-ticking-during-replay is not already blocked (see §5).

### `currentStep` state machine

`ViewerStep = 'video' | 'sections' | 'quiz' | 'signature' | 'complete'` (`TalkViewer.tsx:47`).

- **Available steps** are computed by `getAvailableSteps(talk)` (`TalkViewer.tsx:211-238`): video (if has video), sections (unless standalone-video talk), quiz (if `requiresQuiz && questions.length>0`), signature (always), complete (always).
- **Initial step** is set exactly once via a `React.useEffect` gated by `initialStepSet` (`TalkViewer.tsx:241-272`), so later data refetches (e.g. after a video-progress mutation invalidates the query) never yank the user back to an earlier step.
  - Line 249-252: **if `talk.status === 'Completed' && talk.completedAt`, `currentStep` is forced to `'complete'` and the function returns immediately** — this is the exact logic the prior recon flagged, and it is what routes every completed-talk view straight to `CompletionSuccess` today.
  - Otherwise, the effect walks video→sections→quiz→signature based on completion flags on `talk` (`videoComplete`, `sectionsComplete`, `quizComplete`).
- **Step transitions** are driven by four scattered call sites, not a single reducer: the video step's inline "Continue" button `onClick` (`TalkViewer.tsx:573-611`), `handleNextSection` (`TalkViewer.tsx:373-386`), `QuizSection`'s `onContinue` prop wired to `() => setCurrentStep('signature')` (`TalkViewer.tsx:643`), and `handleComplete` itself setting `'complete'` after a successful completion (`TalkViewer.tsx:355`). All three of the first group currently point at either `'sections'`, `'quiz'`, or `'signature'` as the "next" step — this is where review mode must redirect the terminal target from `'signature'` to `'complete'` (§4).
- **Render dispatch** (`TalkViewer.tsx:440-474` and `544-654`) is a flat set of `currentStep === X &&` blocks, not a state-machine library — consistent with the "scattered but shallow" character of the whole component.

### CompletionSuccess.tsx

Renders unconditionally once `currentStep === 'complete'`: a green success banner, a stats grid (completed date, time spent, video-watch%, quiz score — `CompletionSuccess.tsx:135-167`), a signature-confirmation block (`172-192`), a conditional certificate-download section (`195-214`, gated on `completion.certificateUrl`), and a "Back to My Learnings" footer button (`217-224`). There is no button anywhere in the file that reopens content today.

The most natural home for a "Review this learning" button is **inside the existing `CardFooter`, alongside "Back to My Learnings"** (`CompletionSuccess.tsx:217-224`) — same visual weight, same place the user's eye already lands after reading the summary. A new optional prop (e.g. `onReview?: () => void`) rendered as a second button in that footer is the minimal change.

### Backend endpoints TalkViewer's mutations hit — write/read summary

Already covered in the table above. Net: **every content-fetching call is read-only** (`GetMyToolboxTalkByIdQuery` and the subtitle/slide/slideshow GETs it indirectly triggers); **every mutation writes**, and four of the five mutations already reject writes against a `Completed` scheduled talk at the handler level.

## 3. Part 2 — Shape 2 clean-implementation check

### Can TalkViewer accept a mode flag without significant refactor? Yes.

Only one piece of new state is needed: `const [reviewMode, setReviewMode] = React.useState(false)`. Everywhere it needs to be consulted:

1. **`getAvailableSteps`** — accept `reviewMode` and omit the `signature` step from the returned array when true. This is the single most load-bearing change: it removes Sign from the `StepIndicator`/`MobileStepSelector` UI, makes it unclickable, and (combined with #2) means the live `SignatureCapture` component is never mounted during review.
2. **The three "advance past the last content step" call sites** (video step's Continue button, `handleNextSection`, `QuizSection`'s `onContinue`) — each currently hard-codes `'signature'` as its "next" target when there's nothing left; change each to `reviewMode ? 'complete' : 'signature'`. Three one-line edits, not a refactor.
3. **`handleVideoProgress`** — add an early return when `reviewMode` is true, so video replay never attempts a network call that the backend would reject (see §5 for why this one specifically needs an explicit guard, unlike the others).
4. **A persistent "Review mode" banner** — one conditional block near the top of the main return (`TalkViewer.tsx:479` area), rendered when `reviewMode` is true. Purely additive JSX, no interaction with existing logic.
5. **Entry point** — a new handler, e.g.:
   ```
   const handleStartReview = () => {
     setReviewMode(true);
     const steps = getAvailableSteps(talk); // reviewMode still false here on purpose — see note
     setCurrentStep(steps[0]?.key ?? 'sections');
   };
   ```
   passed to `CompletionSuccess` as `onReview={handleStartReview}`. This does **not** reuse the initial-step effect (`TalkViewer.tsx:241-272`) — that effect's job is "skip whatever's already done," which is the opposite of what review mode wants (it wants to walk through everything, including already-watched video and already-read sections, on purpose).

**Signature step disabling does not need any change inside `SignatureCapture.tsx` itself.** Because step 1 removes `'signature'` from `availableSteps`, and the render block at `TalkViewer.tsx:649-654` is gated on `currentStep === 'signature'`, that branch simply never executes in review mode — `SignatureCapture` is never mounted, so it needs no awareness of review mode at all.

### The "no completion state at end of review" question — resolved, not underspecified

Because `talk.completedAt` is already set (it's a real, already-completed `ScheduledTalk`), routing the terminal transition to `'complete'` instead of `'signature'` lands back on the **exact same `CompletionSuccess` render branch** (`TalkViewer.tsx:440-474`) that the page showed before the user clicked "Review" — same completion date, same score, same certificate link, because that branch reads directly off `talk`, not off any new completion object. **No new terminal UI needs to be built.** The operator's journey is: CompletionSuccess → Review → (video/sections/quiz, walked through read-only) → CompletionSuccess again. This is a closed loop, not a dead end.

One cosmetic follow-up worth doing (not required for correctness): the banner from §2 item 4 staying visible on this second `CompletionSuccess` render would read oddly ("Review mode" banner over a "Learning Completed!" card). Simplest fix: clear `reviewMode` back to `false` at the same point `currentStep` is set to `'complete'` in the review-exit paths, since nothing downstream depends on `reviewMode` remaining true once the loop closes.

### Mutations that cannot be safely suppressed — none found

Every write-mutation TalkViewer can trigger is either (a) already unreachable in review mode because the underlying component short-circuits on "already done" state (`SectionContent`, `QuizSection`), or (b) trivially guarded with an early return with no loss of functionality (`handleVideoProgress`). There is no mutation where suppressing it would leave the UI in a broken or inconsistent state — the backend has no server-side counter or side effect that *needs* to fire for review purposes (e.g. there's no "view count" or "last reviewed at" field anywhere in `ScheduledTalk` or `ScheduledTalkCompletion` today, so there's nothing review mode is expected to record).

**"Read for review" vs. read-only side effect** (the question posed in scope item 5): there is no existing concept of tracking "reviewed" separately from "completed," and nothing in the codebase expects one (no `LastReviewedAt` field, no reporting surface reads one). Treat suppressing section-read tracking during review as pure no-op, not as a gap — recording review activity was explicitly out of scope per the design decision ("no compliance impact").

### Route or derived state? Derived local state — no route/URL change

`reviewMode` can live entirely as `TalkViewer` component state, exactly as scoped in the prompt's example. No new Next.js route, no `?mode=review` query param, no server component change. This also means: no deep-linking to "review mode" directly (e.g. can't bookmark a URL that opens straight into review) — acceptable, since the only entry point is the button on `CompletionSuccess`, which the operator always passes through first.

## 4. Part 3 — Backend surface

**Answer: (a) — no backend changes.**

- `GetMyToolboxTalkByIdQueryHandler` has no status filter (`.Where(st => st.Id == ... && st.TenantId == ... && st.EmployeeId == ... && !st.IsDeleted)`, lines 40-43) — it already returns full section content (translated, in order), full question text/options (correct answers excluded from the DTO regardless of status — `MyToolboxTalkQuestionDto` has no `CorrectAnswer`/`CorrectOptionIndex` field), video URL, slideshow flag, and the existing completion snapshot (`CompletedAt`, `CertificateUrl`) for a `Completed` scheduled talk exactly as for any other status. Confirmed directly by re-reading the handler in this session (not assumed from the prior recon).
- Quiz question randomization (`hasRandomization && talk.RequiresQuiz && !quizAlreadyPassed`, line 91) — for a completed talk, `quizAlreadyPassed` is `true` (a required quiz cannot be completed unless passed), so the handler takes the **non-randomized, original-order path** every time the completed talk is reopened. This is stable and desirable for review — the operator isn't shown a shuffled quiz they can't submit anyway (moot, since `QuizSection`'s `alreadyPassed` branch never renders the questions at all — see §1).
- `StartToolboxTalkCommandHandler`'s existing silent no-op for `Completed`/`Cancelled` status (lines 61-65) means a review-mode client calling `/start` (which it won't, per §3 item 1 in Part 2, but even if it did) is already safe today with zero new code.

Section 8 of the prompt's scope is confirmed: section content, video URL, and quiz question data are all already fetchable via `GetMyToolboxTalkByIdQueryHandler` in a form `TalkViewer` already consumes today (it's the same `MyToolboxTalkDto` that powers the live flow) — no new endpoint, no new DTO field, no new query needed.

## 5. Files-to-change list

| File | Change |
|---|---|
| `web/src/features/toolbox-talks/components/TalkViewer.tsx` | Add `reviewMode` state; thread it into `getAvailableSteps` (drop `signature` step when true); redirect the three "advance to next step" call sites to `'complete'` instead of `'signature'` when in review mode; guard `handleVideoProgress` with an early return in review mode; add `handleStartReview` entry handler passed to `CompletionSuccess`; render a persistent review-mode banner; clear `reviewMode` when looping back to `'complete'`. |
| `web/src/features/toolbox-talks/components/CompletionSuccess.tsx` | Add optional `onReview?: () => void` prop; render a "Review this learning" button in the existing `CardFooter` alongside "Back to My Learnings". |

No other frontend component and no backend file needs to change.

## 6. Edge cases flagged

1. **Video-progress ticks during replay can still reach a throwing endpoint if unguarded.** Traced in `VideoPlayer.tsx:355-389`: `watchPercent` and `lastReportedPercent` both initialize from `currentWatchPercent` (the already-saved value), and `effectivePercent = Math.max(computedPercent, currentWatchPercent)` — so a full, uninterrupted replay-from-zero **can** compute a `watchPercent` slightly above the originally-stored value (e.g. stored 91% from a session with a pause; a clean full replay computes 100%), which would fire `onProgressUpdate` and hit `UpdateVideoProgressCommandHandler`'s "already completed" throw. The failure is silently swallowed by `VideoPlayer`'s own `.catch()` (line 382-384) and again by `TalkViewer`'s `handleVideoProgress` catch block, so **nothing breaks visibly** — but it is an unnecessary failing network call and a server-side exception logged for no reason on every review session with a video. This is exactly why `handleVideoProgress` needs an explicit `reviewMode` guard rather than relying on "it's harmless anyway."
2. **Operator mid-review when the talk gets un-completed by an admin edge case.** Not a currently-reachable path (no admin action un-completes a `ScheduledTalk` today — cancellation is blocked once completed, per the prior recon's edge-case table), so this is theoretical. If it ever became reachable, the risk is that `talk.completedAt` would go missing mid-session while `reviewMode` is still true and `currentStep` might be `'quiz'` or `'sections'` — the exit-to-`'complete'` transition would then render nothing (the `currentStep === 'complete' && talk.completedAt` guard would fail with `completedAt` now null) and the user would be stuck on whatever step they were on with a stale `reviewMode` flag. Flagging only; no action recommended since the precondition doesn't currently exist.
3. **Review mode for a talk with no video and no quiz.** `getAvailableSteps` would produce just `sections` (or nothing, for a standalone-video talk with no sections — but that case by definition *has* a video) plus `complete`. Reviewing in this case is just re-reading the section text, which is exactly what "review the content again" means for a talk that's pure text content — not a degenerate case, just a shorter loop.
4. **Version/content drift — the talk was edited since the operator completed it.** Confirmed as a real gap, not a hypothetical: `GetMyToolboxTalkByIdQueryHandler` always projects from the **live** `ToolboxTalk.Sections`/`ToolboxTalk.Questions` tables (lines 30-36, 62-81, 100-116), and `ScheduledTalkCompletion` (the entity that records what happened at completion time) stores no content snapshot at all — only score/time/signature/geolocation metadata (`ScheduledTalkCompletion.cs:10-99`, confirmed by direct read: no `SectionsSnapshotJson` or similar field exists). So if an admin edits a section's wording, adds/removes quiz questions, or replaces the video after the operator completed the talk, **review mode will show the current content, not what the operator actually completed and signed for**. This is true of the existing "reopen an in-progress talk" flow too (it's not new risk introduced by review mode — it's a pre-existing property of how the DTO is built), but review mode is the first feature that deliberately invites operators to look at old completions again, which makes the drift more visible/likely to be noticed than it is today. **This is a genuine, smaller design question** worth a one-line confirmation from the boss before building: is "show current content" acceptable, or does "review" implicitly promise "review what I actually signed for"? Recommend surfacing this as a single yes/no rather than reopening the earlier six-question list.

## 7. Recommended next step

Shape 2 holds up cleanly under this recon — write the implementation prompt now. The surface is fully identified: two files, no backend changes, and the specific line-level touch points are enumerated in §5 above. The only open item before or alongside implementation is the version-drift question in §6 item 4, which is small enough to fold into the implementation prompt itself as a stated assumption ("review shows current content, not a point-in-time snapshot — flag to boss as a known limitation") rather than blocking on a separate design conversation.
