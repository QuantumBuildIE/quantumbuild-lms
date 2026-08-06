# Assignment Notification Emails — Recon

Date: 2026-07-15
Scope: read-only recon, no code changes. Verified against code at HEAD on `transval` (latest commit `f24bfb4`). Builds on three same-week recons already in the repo (`docs/refresher-notification-recon.md`, `docs/scheduled-refresh-flow-recon.md`, `docs/reminder-employee-scoping-recon.md`, all dated 2026-07-08) — every claim below was re-verified against current files, not cited blind.

## Headline

**The most likely root cause is not the refresher-notification work that landed this week — it's a long-standing, unrelated bug in the standalone-talk scheduling flow that has existed since the module's creation on 2026-02-16.** Schedules created via the Learnings-list "Schedule" action are saved with `Status = Draft`, but the nightly job that turns schedules into actual assignments (and sends the assignment email) only ever picks up schedules with `Status == Active`. Nothing in the create flow ever flips that status, and the frontend never calls the one manual endpoint that would. Net effect: for the most common admin action — "assign this learning to these employees, starting today" — **no `ScheduledTalk` row is ever created and no email is ever sent, indefinitely, until an admin happens to separately open the schedule and click "Process Now."** This is independent of anything changed this week.

Separately, this week's "refresh flow item 1" commit (`08a1801`) is real and does exactly what the commit message says — it added creation-time assignment emails to the *refresher* scheduling path, which previously had none. That is a different code path from "admin assigns a new learning" and was not broken before or after the change; it's an added feature, not a fix to the reported symptom, unless the reports are specifically about refresher assignments.

A third, independent possibility undermines any test result either way: the email provider defaults to a **stub that logs and reports success without sending anything**, and no `EmailProvider` configuration was found checked into any `appsettings*.json` in the repo. Whether this reproduces depends entirely on environment variables not visible from the repo.

---

## Part 1 — Notification code path

There is no single "assignment created" domain event or centralized notification dispatcher. Each creation path calls `IToolboxTalkEmailService` (or doesn't) directly, inline, in its own command handler.

### Interface & implementation

- `IToolboxTalkEmailService` — `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/IToolboxTalkEmailService.cs`. Two relevant methods:
  - `SendTalkAssignmentEmailAsync(ScheduledTalk scheduledTalk, Employee employee, CancellationToken ct = default)` — [IToolboxTalkEmailService.cs:14-17](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/IToolboxTalkEmailService.cs#L14)
  - `SendCourseAssignmentEmailAsync(ToolboxTalkCourse course, Employee employee, int talkCount, DateTime? dueDate, CancellationToken ct = default)` — [IToolboxTalkEmailService.cs:57-62](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/IToolboxTalkEmailService.cs#L57)
- Implementation: `ToolboxTalkEmailService` — `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkEmailService.cs`
  - `SendTalkAssignmentEmailAsync` body: [ToolboxTalkEmailService.cs:40-117](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkEmailService.cs#L40)
  - Email-address gate: `if (string.IsNullOrEmpty(employee.Email)) { _logger.LogWarning(...); return; }` — [line 45-51](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkEmailService.cs#L45). This is the **only** per-employee gate inside the email service itself.
  - Dispatch: `var result = await _emailProvider.SendAsync(emailMessage, cancellationToken);` — [line 103](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkEmailService.cs#L103), then logs success/failure (lines 105-116) — a failure here is **logged only**, never thrown, never surfaced to the caller as anything but a log line.
  - This service is distinct from `ToolboxTalkNotificationService` (same Infrastructure folder), which handles **admin-facing** pipeline notifications (translation/validation complete, gated by `ToolboxTalkSettings.NotifyOnTranslationComplete` etc. — [ToolboxTalkSettings.cs:117-120](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalkSettings.cs#L117)) and is unrelated to employee assignment emails.
  - Also distinct from the shared Core `EmailService`/`IEmailService` (`src/Core/QuantumBuild.Core.Infrastructure/Services/EmailService.cs`), which only handles password-setup, user-created, and external-review-invitation emails.

### Gates confirmed present

- **Tenant-level notification toggle:** none found. `ToolboxTalkSettings` has no flag gating `SendTalkAssignmentEmailAsync`/`SendCourseAssignmentEmailAsync` — confirmed by full-file read and by `docs/refresher-notification-recon.md` §9 (independently re-verified here).
- **Employee-level notification preference:** none found anywhere in the module.
- **Employee active status:** not checked inside the email service. It *is* checked upstream, at candidate-selection time, in some (not all) of the creation paths — see Part 2 per-flow breakdown.
- **Employee email present:** checked, per above (`ToolboxTalkEmailService.cs:45-51`).
- **Learning active status:** `CreateToolboxTalkScheduleCommandHandler` rejects scheduling an inactive talk outright — `if (!toolboxTalk.IsActive) throw ...` — [CreateToolboxTalkScheduleCommandHandler.cs:42-45](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L42). Not relevant to whether an *active* talk's assignment email fires.
- **Try/catch swallow around every send call**, consistently, across every creation path that sends an email — a send failure never rolls back or blocks the assignment. See per-flow citations in Part 2.

---

## Part 2 — Assignment creation flows

Ten `.Add()` call sites create `ScheduledTalk` or `ToolboxTalkCourseAssignment` rows across the whole module (confirmed via exhaustive grep — no `AddRange` usage exists anywhere). Six distinct flows:

| # | Flow | Entry point | Creates row(s) | Sends email? |
|---|---|---|---|---|
| 1 | Schedule a learning (Learnings list "Schedule" action) | `POST /api/toolbox-talks/schedules` → `CreateToolboxTalkScheduleCommandHandler` | `ToolboxTalkSchedule` + `ToolboxTalkScheduleAssignment` placeholders only — **no `ScheduledTalk` yet** | N/A at this step |
| 2 | Schedule processing (turns #1 into real assignments) | `ProcessToolboxTalkSchedulesJob` (daily 6:30am) or manual `POST /schedules/{id}/process` → `ProcessToolboxTalkScheduleCommandHandler` | `ScheduledTalk` | **Yes** — pre-existing, not part of this week's work |
| 3 | Course assignment (Course edit page → "Assign Employees") | `POST /api/toolbox-talks/course-assignments` → `AssignCourseCommandHandler` | `ScheduledTalk` (per item) + `ToolboxTalkCourseAssignment` | **Yes** — pre-existing, not part of this week's work |
| 4 | Refresher scheduling (on talk/course completion) | `CompleteToolboxTalkCommandHandler` / `CourseProgressService` → `RefresherSchedulingService` | `ScheduledTalk` and/or `ToolboxTalkCourseAssignment` | **Yes — added this week** (commit `08a1801`) |
| 5 | New-employee auto-assignment | `EmployeeService.CreateAsync` → `AutoAssignmentService` | `ScheduledTalk` and/or `ToolboxTalkCourseAssignment` | **No — never has** |
| 6 | QR on-site attendance completion | `QrScanController.Complete` | `ScheduledTalk` (pre-completed) | **No** (and also skips refresher scheduling entirely — see note below) |

### Flow 1+2 in detail — the one that matters most

This is the flow behind "admin assigns a new learning to an operator" for a **standalone** (non-course) talk — the Learnings list's Actions-menu "Schedule" item is the only admin surface for this (`ToolboxTalkList.tsx:308-319`, dispatched via the `onSchedule` prop into `ScheduleDialog.tsx`, used from `web/src/app/(authenticated)/admin/toolbox-talks/talks/page.tsx`). There is no other direct "assign one talk to one/many employees, right now" endpoint — `ScheduledTalksController` (`/api/toolbox-talks/assigned`) is read-only plus reminder/cancel; confirmed no POST-create action exists there.

**Step 1 — `ScheduleDialog.tsx` submit → `CreateToolboxTalkScheduleCommandHandler`:**
```csharp
// CreateToolboxTalkScheduleCommandHandler.cs:107-119
var schedule = new ToolboxTalkSchedule
{
    ...
    Status = ToolboxTalkScheduleStatus.Draft,
    NextRunDate = request.ScheduledDate,
    ...
};
```
[CreateToolboxTalkScheduleCommandHandler.cs:107-119](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L107) — every newly created schedule starts life as **Draft**, regardless of `ScheduledDate` (even if it's today) or `Frequency` (even if `Once`). This has been true since the module's first commit, `9e575bf` (2026-02-16) — confirmed via `git blame`.

The frontend's submit handler (`ScheduleDialog.tsx:190-239`, specifically `onSubmit` → `await createMutation.mutateAsync(payload)` at [line 221](../web/src/features/toolbox-talks/components/ScheduleDialog.tsx#L221)) does nothing else after this call succeeds — no follow-up call to process/activate the schedule. The success path is just a toast and closing the dialog (lines 222-226).

**Step 2 — turning the Draft schedule into an actual assignment:**

The only two things that can make `ScheduledTalk` rows exist for this schedule are:

- **`ProcessToolboxTalkSchedulesJob`** (Hangfire recurring job, `30 6 * * *` Ireland time, confirmed via `docs/scheduled-refresh-flow-recon.md`), whose query is:
  ```csharp
  var schedulesToProcess = await _dbContext.ToolboxTalkSchedules
      .IgnoreQueryFilters()
      .Where(s => s.TenantId == tenant.Id && !s.IsDeleted)
      .Where(s => s.Status == ToolboxTalkScheduleStatus.Active)
      .Where(s => s.ScheduledDate.Date <= today || ...)
      .ToListAsync(cancellationToken);
  ```
  [ProcessToolboxTalkSchedulesJob.cs:57-63](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ProcessToolboxTalkSchedulesJob.cs#L57) — **requires `Status == Active`.** A schedule sitting in `Draft` is invisible to this query no matter what its `ScheduledDate`/`NextRunDate` is. This line, too, is unchanged since `9e575bf` (2026-02-16) — confirmed via `git blame`.
- **Manual "Process Now"** — `POST /api/toolbox-talks/schedules/{id}/process`, exposed as a "Process Now" action in the schedule's own Actions menu on the Schedules list/detail pages: `ScheduleList.tsx:252-264` (condition: `canSchedule && (item.status === 'Draft' || item.status === 'Active')`) and the schedule detail page (`web/src/app/(authenticated)/admin/toolbox-talks/schedules/[id]/page.tsx:67`). This is the **only** UI surface that can process a Draft schedule, and it lives on a different page from where the schedule was created (the Schedules list, not the Learnings list where "Schedule" was clicked).

**The only code path that ever sets `Status = Active` is inside `ProcessToolboxTalkScheduleCommandHandler` itself, as a side effect of already having processed the schedule:**
```csharp
// ProcessToolboxTalkScheduleCommandHandler.cs:153-166
if (schedule.Frequency == ToolboxTalkFrequency.Once)
{
    schedule.Status = ToolboxTalkScheduleStatus.Completed;   // Once never becomes Active
    ...
}
else
{
    if (schedule.Status == ToolboxTalkScheduleStatus.Draft)
        schedule.Status = ToolboxTalkScheduleStatus.Active;   // only flips AFTER a manual process
    ...
}
```
[ProcessToolboxTalkScheduleCommandHandler.cs:153-166](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L153) — for a `Once`-frequency schedule (the form's default — `ScheduleDialog.tsx:157`, `frequency: schedule?.frequency ?? 'Once'`), the schedule goes `Draft → Completed` directly and is **never** `Active` at any point. For a recurring schedule, it only becomes `Active` after the first manual "Process Now" run — the nightly job cannot be the thing that triggers this transition, because the nightly job's own query already excludes `Draft` rows.

**Net effect, confirmed by reading the code, not inferred:** the default admin workflow — open the Learnings list, click Actions → Schedule on a talk, pick employees and today's date, accept the default "Once" frequency, submit — produces a `ToolboxTalkSchedule` + `ToolboxTalkScheduleAssignment` rows that **no automatic process will ever pick up**. No `ScheduledTalk` row is created, therefore no assignment email is ever sent, therefore the employee never sees the learning in their portal either — not delayed until the next 6:30am run, but permanently stuck, unless an admin separately discovers and clicks "Process Now" on the Schedules list.

This is **not** part of anything changed this week. Both load-bearing lines (`Status = Draft` at creation, `Status == Active` in the job's filter) trace back to the original module-extraction commit `9e575bf` (2026-02-16).

### Flow 3 — Course assignment (immediate, no Draft gate)

`AssignCourseCommandHandler.cs` creates `ScheduledTalk` rows ([line 136](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/Commands/AssignCourseCommandHandler.cs#L136)) and the `ToolboxTalkCourseAssignment` ([line 153](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/Commands/AssignCourseCommandHandler.cs#L153)) synchronously in the request, no schedule/draft indirection at all. `SaveChangesAsync` happens at [line 177](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/Commands/AssignCourseCommandHandler.cs#L177), then the email loop runs immediately after (lines 179-203), one `SendCourseAssignmentEmailAsync` call per employee (not per item). If assignment testing was done via a **course**, not a standalone talk, this flow works today exactly as it has throughout — nothing here was touched this week.

### Flow 5 — New-employee auto-assignment (confirmed no email, unrelated to this week)

`AutoAssignmentService.cs` — full file read. Constructor only takes `IToolboxTalksDbContext` and `ILogger` ([lines 19-25](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/AutoAssignmentService.cs#L19)) — no `IToolboxTalkEmailService` dependency exists. Both the course path ([line 96](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/AutoAssignmentService.cs#L96)) and standalone path ([line 137](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/AutoAssignmentService.cs#L137)) only call `_logger.LogInformation` after adding rows. If "assigning a new learning" in the reports actually means "a new employee was onboarded and got auto-enrolled," there has never been an email here — this is not a regression, it's a gap that has always existed. Not part of this week's change.

### Flow 6 — QR completion (unrelated edge case, flagged for completeness)

`QrScanController.cs:435-474` builds an already-`Completed` `ScheduledTalk` directly with no email call and, notably, without calling `IRefresherSchedulingService` at all — a talk completed via QR that has `RequiresRefresher = true` will not get a refresher scheduled the way the normal completion path does. Unrelated to the reported symptom (this creates a completed record, not a new assignment needing notification), noted only because it surfaced during the flow inventory.

---

## Part 3 — Recent history and configuration

### What actually landed this week ("refresh flow")

Three commits, all by the same author, all dated 2026-07-08 (one week before this recon):

| Commit | Summary | Touches the reported flow? |
|---|---|---|
| `61e2750` | `fix(refresher): guard against Weekly-frequency silent-disable` — guards `UpdateToolboxTalkCommandHandler` so editing a talk with legacy `Frequency = Weekly` doesn't silently flip `RequiresRefresher` to false | No — talk-edit path, not assignment creation |
| `08a1801` | `feat(refresher): send assignment email when refresher is scheduled` — adds `IToolboxTalkEmailService` + `ICoreDbContext` to `RefresherSchedulingService`, sends `SendTalkAssignmentEmailAsync`/`SendCourseAssignmentEmailAsync` after each refresher's own `SaveChangesAsync` | Only the **refresher** path (Flow 4 above) — confirmed via full diff, see below |
| `4129d30` | `fix(reminders): stop emailing soft-deleted employees` — adds `!Employee.IsDeleted` predicate to `SendRefresherRemindersJob` (4 queries) and `SendToolboxTalkRemindersJob` (1 query) to compensate for `IgnoreQueryFilters()` cascading past `Employee`'s soft-delete filter | No — this is the **reminder** job (pre-due nudges for already-existing assignments), not assignment creation |

**Full diff of `08a1801`** confirms the commit message precisely: it adds `ICoreDbContext coreContext, IToolboxTalkEmailService emailService` to the constructor, an `Employee` lookup by ID (`!e.IsDeleted` only, no `IsActive` check — consistent with the rest of the codebase's continuity-notification pattern per `docs/reminder-employee-scoping-recon.md`), and a try/catch-wrapped email call in each of the two `ScheduleRefresherIfRequired` overloads. It does **not** touch `ProcessToolboxTalkScheduleCommandHandler`, `AssignCourseCommandHandler`, `CreateToolboxTalkScheduleCommandHandler`, or `ProcessToolboxTalkSchedulesJob` — none of the code behind Flows 1-3 above changed this week at all.

**Conclusion for Part 3:** if the "reports" being investigated are about *newly hired or newly assigned standalone learnings* (Flow 1+2), this week's work is irrelevant to them — that code path is untouched and has had the Draft/Active gap since February. If the reports are specifically about *refresher* assignments (an operator due for a repeat), this week's work is exactly on point and should be verified against its own test suite (`RefresherSchedulingServiceTests.cs`, 6 tests, added in the same commit) and a live smoke test, since the existing recon docs flagged EF navigation-fixup and Employee-lookup risk that unit tests may not fully cover in a live environment (real DB round-trip, real email provider).

### Hangfire job history / retry visibility

- `ProcessToolboxTalkSchedulesJob.ExecuteAsync` is `[AutomaticRetry(Attempts = 3)]` ([line 39](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ProcessToolboxTalkSchedulesJob.cs#L39)) and wraps per-tenant and per-schedule processing in try/catch that logs and continues (lines 92-101, 104-111) — a single schedule's exception does not prevent other schedules from processing, and does not surface anywhere outside the Hangfire dashboard's job history / application logs.
- Given the Draft/Active gap above, **Hangfire's job history for this job will show successful runs with zero relevant schedules found** for any tenant whose schedules are all still in Draft — there is nothing to see in Hangfire that would look like a failure, because the job is working exactly as coded; it simply has nothing to process.
- No test coverage exists for `ProcessToolboxTalkSchedulesJob` or `CreateToolboxTalkScheduleCommandHandler`'s Draft/Active interaction specifically (not confirmed exhaustively, but no test file matching either name surfaced during this recon).

---

## Part 4 — Environment-specific behaviour

### Email provider selection

```csharp
// Program.cs:128-141
var emailProvider = builder.Configuration.GetValue<string>("EmailProvider:Provider");
if (string.Equals(emailProvider, "MailerSend", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IEmailProvider, MailerSendEmailProvider>(...);
}
else
{
    builder.Services.AddSingleton<IEmailProvider, StubEmailProvider>();
}
```
[Program.cs:128-141](../src/QuantumBuild.API/Program.cs#L128) — **no `EmailProvider` section was found in `appsettings.json`, `appsettings.Development.json`, or `appsettings.Testing.json`** anywhere in the repo (confirmed via grep across the whole `src/QuantumBuild.API` tree, excluding `bin`/`obj`). `EmailProviderSettings`'s own C# default is `Provider = "Stub"` ([EmailProviderSettings.cs:7](../src/Core/QuantumBuild.Core.Infrastructure/Services/Email/EmailProviderSettings.cs#L7)). Unless a Railway/local environment variable `EmailProvider__Provider=MailerSend` (plus `EmailProvider__ApiKey`) is set outside the repo, **every environment defaults to `StubEmailProvider`.**

```csharp
// StubEmailProvider.cs:17-24
public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
{
    _logger.LogInformation("[StubEmailProvider] Email logged (not sent) — To: {To}, Subject: {Subject}, BodyLength: {Length}", ...);
    return Task.FromResult(EmailSendResult.Succeeded("stub-" + Guid.NewGuid().ToString("N")[..8]));
}
```
[StubEmailProvider.cs:17-24](../src/Core/QuantumBuild.Core.Infrastructure/Services/Email/StubEmailProvider.cs#L17) — **this always reports success.** Every log line upstream (`ToolboxTalkEmailService.cs:107-109`, `RefresherSchedulingService`'s own logging) will say the email "sent successfully" even though nothing left the process. This is a real, first-order risk to any test conducted without independently confirming which provider is actually active in the environment under test — application logs alone cannot distinguish "really sent via MailerSend" from "stubbed" without checking the configured provider, since the log messages for the two providers are worded almost identically at the call site (`ToolboxTalkEmailService.cs:107` doesn't know or care which provider ran).

**This recon cannot determine, from the repo alone, whether Development/Demo/Production Railway services currently have `EmailProvider__Provider=MailerSend` set.** That is an operational fact that must be checked directly (Railway dashboard or `railway variables`), not inferred from code.

### From-address / deliverability

`EmailProviderSettings.FromEmail` defaults to `"noreply@quantumbuild.ie"` ([EmailProviderSettings.cs:9](../src/Core/QuantumBuild.Core.Infrastructure/Services/Email/EmailProviderSettings.cs#L9)) unless overridden. Whether `quantumbuild.ie` has valid outbound-mail DNS records (SPF/DKIM) for MailerSend to use, and whether that domain is actually verified in the MailerSend account, are both facts external to this codebase and unverifiable from a repo read — flagging per the brief's concern about non-deliverable senders, not resolving it.

### Failure-mode silence

`MailerSendEmailProvider.SendAsync` ([MailerSendEmailProvider.cs:27-82](../src/Core/QuantumBuild.Core.Infrastructure/Services/Email/MailerSendEmailProvider.cs#L27)) logs a warning and returns `EmailSendResult.Failed(...)` on any non-2xx MailerSend response (including HTTP 429 — rate limiting, a known BACKLOG item per CLAUDE.md's Backlog/Medium section: "MailerSendEmailProvider 429 handling"). This failure is then logged again (as a warning, not error) by `ToolboxTalkEmailService.cs:113-115`, and swallowed entirely by the try/catch at every call site (Flows 2, 3, 4). **A dropped email due to 429, an expired API key, an unverified sender domain, or any other MailerSend-side rejection produces zero user-facing or admin-facing signal** — only a log line that nobody is necessarily watching.

---

## Diagnosis

Four independent, non-exclusive explanations were investigated. In order of how strongly the evidence in the repo supports each as the actual cause of "operators are not receiving assignment emails":

1. **Strongest, most concrete: the reported flow is standalone-talk scheduling, and the schedule is stuck in Draft.** If the admin used the Learnings-list "Schedule" action (the primary, most likely-to-be-tested surface for "assign a new learning"), no `ScheduledTalk` row — and therefore no email — is ever created unless someone separately clicks "Process Now" on the Schedules list. This reproduces 100% of the time for this flow, for every tenant, and has done so since February. This is a **flow that skips the notification entirely**, per the brief's framing — it's not that the email fails to send, it's that the code path that would send it never runs.
2. **Plausible but requires an environment check: emails are firing but the Stub provider is active.** If `EmailProvider__Provider` isn't set to `MailerSend` in whatever environment was tested, every "assignment email sent" log line is true only in the sense that a stub logged it — nothing was actually delivered, on any flow (schedule, course, refresher, alike). This would make every flow "look right" in logs while nothing arrives in an inbox.
3. **Plausible only if the reports are specifically about refresher assignments: this week's `08a1801` change is new, lightly-tested-in-anger code.** Unit tests exist (6 cases) but there is no confirmed evidence in this recon of a live smoke test against a real DB + real employee + real email provider. If the reports are about operators not getting notified when their *repeat/refresher* training comes up, this is the code to scrutinize first — check whether the `Employee` lookup succeeds, whether `refresher.ToolboxTalk = talk` avoids a null-nav exception, and whether the email actually reaches `SendAsync`.
4. **Weakest as a general explanation, but real if MailerSend is genuinely configured: silent MailerSend failures (429, bad API key, unverified domain).** Would explain intermittent, not total, failure — "some operators got it, some didn't" — and would look identical to success in the absence of someone reading Warning-level logs.

None of these are mutually exclusive — for example, explanation 1 (Draft-stuck schedules) would make a tester conclude "no email arrived" without ever reaching the point where explanations 2-4 would even matter, since no email attempt happens in that flow at all.

**What this recon rules out:** a tenant-level or code-level notification gate silently disabling assignment emails. No such gate exists anywhere in `ToolboxTalkSettings` or the email service itself — the four things the brief asked about explicitly (tenant flag, employee preference, employee active status, learning active status) were all checked and none of them block a *reachable* `SendTalkAssignmentEmailAsync`/`SendCourseAssignmentEmailAsync` call. The gaps found are about **whether the call happens at all** (Draft schedules, auto-assignment) and **whether delivery actually occurs once dispatched** (Stub provider, silent MailerSend failures), not about a gate rejecting an otherwise-correct send.

---

## Recommended verification steps

In priority order, cheapest/most-decisive first:

1. **Reproduce the exact flow the reports are about.** Ask whoever filed the report: did they use the Learnings-list "Schedule" action on a standalone talk, or a course assignment, or is this about a refresher/repeat training coming due? The diagnosis differs completely by flow — this single question resolves most of the ambiguity above.
2. **Check `ToolboxTalkSchedules.Status` in the database** for the specific schedule(s) the report is about. If `Status = 'Draft'` and the employee has no corresponding `ScheduledTalk` row, explanation 1 is confirmed on the spot — no further investigation needed, this is the bug.
3. **Confirm the active `IEmailProvider` in the environment under test.** Check the Railway service's environment variables (or local `appsettings.Development.json`/user-secrets/env vars) for `EmailProvider__Provider`. If it's unset or anything other than `MailerSend`, every email in every flow is a no-op regardless of any other finding — check this before spending more time on flow-specific debugging.
4. **If Stub is ruled out, grep application logs for `"MailerSend API returned"` or `"Failed to send email via MailerSend"`** around the time of the reported failures — this distinguishes "never attempted" (nothing in logs, points back to #2) from "attempted and rejected by MailerSend" (points to explanation 4).
5. **For the refresher path specifically:** complete a talk configured with `RequiresRefresher = true` end-to-end in a lower environment, confirm a new `ScheduledTalk` with `IsRefresher = true` appears, and check the application log immediately after for the `"Toolbox Talk assignment email sent to {Email}"` line from `ToolboxTalkEmailService.cs:107-109` (or its absence, or an `"employee not found"` error from `RefresherSchedulingService.cs:73-76`).
6. **If a real send is confirmed dispatched with no report-side receipt**, check the receiving mailbox's spam/junk folder and confirm the test tenant's employee email address is a real, monitored inbox and not a placeholder (e.g. a seeded test employee with a non-existent address) — this is a separate, mundane failure mode not covered by anything above.

No fix is proposed here per the recon brief — this document is diagnostic only.
