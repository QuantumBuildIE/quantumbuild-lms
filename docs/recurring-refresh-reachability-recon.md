# Recurring Schedule Refresh Reachability — Recon

Read-only recon. No production code changed. A temporary characterisation test
(`ReconRecurringRefreshReachabilityTests.cs`) was added, run against a real
Postgres Testcontainers instance through the actual HTTP/command-handler stack,
and then deleted — its result is quoted verbatim in §B. All other claims are
`file:line` against the current `transval` branch working tree.

## Headline

**Hypothesis CONFIRMED, and worse than stated.** Two independent defects compound:

1. **The recurring refresh block is unreachable in normal operation.** The guard
   `if (!unprocessedAssignments.Any())` can only pass if a schedule's assignments
   are *externally* left in an all-processed state at the moment `/process` runs
   — but every non-completing recurring run unconditionally resets **all**
   assignments back to `IsProcessed = false` at the end of that same run
   ([ProcessToolboxTalkScheduleCommandHandler.cs:195-201](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L195-L201)). The only way the existing test suite gets the guard to pass is by directly writing to the database to force it (`MarkAllAssignmentsProcessedAsync`, see §E) — a state the real code path never produces on its own.
2. **This is not merely "refresh never fires" — it's "the schedule is (re)processed on every job run, not on its intended cadence."** `ScheduledDate` is set once at creation and never advanced by anything ([confirmed absence of any writer](#a2-what-the-command-handler-does-per-run-in-full-order)). The daily job's due-filter is an **OR** of `ScheduledDate.Date <= today` and `NextRunDate <= today` ([ProcessToolboxTalkSchedulesJob.cs:61-62](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ProcessToolboxTalkSchedulesJob.cs#L61-L62)). Once `ScheduledDate` is in the past — which is true from the day after creation onward for every schedule — the schedule is selected as "due" **every single day**, regardless of `NextRunDate`/frequency. Because the assignments were just reset to unprocessed at the end of the *previous* run, the handler doesn't skip this early call — it reprocesses every currently-assigned employee again, creating a **new duplicate `ScheduledTalk` and a new duplicate assignment email** for each of them, every day, instead of once per week/month/year as configured.

Both the all-employees and department/site-targeted refresh paths are affected
identically — they share the exact same unreachable guard (§A.2).

---

## A. The full recurring-schedule lifecycle

### A.1 — Job's "due" selection

`ProcessToolboxTalkSchedulesJob.ExecuteAsync`, run daily via Hangfire cron `"30 6 * * *"` (Ireland time) — [Program.cs:474-478](../src/QuantumBuild.API/Program.cs#L474-L478):

```csharp
recurringJobManager.AddOrUpdate<ProcessToolboxTalkSchedulesJob>(
    "process-toolbox-talk-schedules",
    job => job.ExecuteAsync(CancellationToken.None),
    "30 6 * * *", // Run at 6:30 AM daily
    new RecurringJobOptions { TimeZone = irelandTimeZone });
```

The due-filter, per tenant — [ProcessToolboxTalkSchedulesJob.cs:57-63](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ProcessToolboxTalkSchedulesJob.cs#L57-L63):

```csharp
var schedulesToProcess = await _dbContext.ToolboxTalkSchedules
    .IgnoreQueryFilters()
    .Where(s => s.TenantId == tenant.Id && !s.IsDeleted)
    .Where(s => s.Status == ToolboxTalkScheduleStatus.Active)
    .Where(s => s.ScheduledDate.Date <= today ||
               (s.NextRunDate.HasValue && s.NextRunDate.Value.Date <= today))
    .ToListAsync(cancellationToken);
```

Every matched schedule is then dispatched through `ProcessToolboxTalkScheduleCommand`
via `_mediator.Send(...)` inside a per-schedule try/catch ([ProcessToolboxTalkSchedulesJob.cs:71-101](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ProcessToolboxTalkSchedulesJob.cs#L71-L101)) — no due-date recheck happens inside the command; the job's filter is the *only* gate.

**`ScheduledDate` is write-once.** It is set at schedule creation —
[CreateToolboxTalkScheduleCommandHandler.cs:175,182](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L175):
```csharp
var schedule = new ToolboxTalkSchedule
{
    ...
    ScheduledDate = scheduledDate,
    ...
    NextRunDate = scheduledDate,
    ...
};
```
and can only be changed afterward by an explicit admin edit through
`UpdateToolboxTalkScheduleCommandHandler.cs:173` (`schedule.ScheduledDate = scheduledDate;`,
a user-initiated PUT). **`ProcessToolboxTalkScheduleCommandHandler` never assigns
`schedule.ScheduledDate` anywhere** — confirmed by reading the full 356-line file;
only `schedule.NextRunDate` is advanced (§A.2). This was independently re-confirmed
at runtime in §B: after one process run, the DB row's `ScheduledDate` was
unchanged while `NextRunDate` had moved a week out.

**Consequence:** for any `Active`, non-`Once` schedule, `ScheduledDate.Date <= today`
becomes permanently `true` starting the day after creation (or immediately, if
created for today/a past date) and never becomes false again for the life of the
schedule. The `NextRunDate` half of the `OR` is therefore decorative for
already-past-`ScheduledDate` schedules — the job selects them as due on every run
regardless of what `NextRunDate` says.

### A.2 — What the command handler does per run, in order

`ProcessToolboxTalkScheduleCommandHandler.Handle` ([full file](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs)):

1. Load the schedule with `Assignments` and `ToolboxTalk.Sections` ([:39-43](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L39-L43)). Reject if `Cancelled`/`Completed` ([:51-59](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L51-L59)).
2. `unprocessedAssignments = schedule.Assignments.Where(a => !a.IsProcessed)` ([:74-76](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L74-L76)).
3. **The refresh gate** ([:78-95](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L78-L95)):
   ```csharp
   if (!unprocessedAssignments.Any())
   {
       if (schedule.AssignToAllEmployees && schedule.Frequency != ToolboxTalkFrequency.Once)
           await RefreshAssignmentsForAllEmployees(...);
       else if (!schedule.AssignToAllEmployees
           && (schedule.TargetDepartmentIds.Any() || schedule.TargetSiteIds.Any())
           && schedule.Frequency != ToolboxTalkFrequency.Once)
           await RefreshAssignmentsForTargetCriteria(...);
   }
   ```
   Both refresh methods are **only** reachable through this one gate — there is no other call site for either ([confirmed by grep](#) — `RefreshAssignmentsForAllEmployees`/`RefreshAssignmentsForTargetCriteria` each appear exactly once, as their own definition, plus this one call each).
4. `foreach (var assignment in unprocessedAssignments)` ([:101-160](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L101-L160)) — **unconditionally**, for every assignment in the (possibly refreshed) unprocessed set: creates a new `ScheduledTalk` + per-section `ScheduledTalkSectionProgress` rows, marks the assignment `IsProcessed = true`/stamps `ProcessedAt`, and sends `SendTalkAssignmentEmailAsync` (try/catch-swallowed on failure, [:148-158](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L148-L158)). Nothing here checks whether "now" has actually reached `NextRunDate` — the handler trusts its caller entirely.
5. Recurrence handling ([:162-202](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L162-L202)):
   - `Once` → `Status = Completed`, `NextRunDate = null`, done.
   - Recurring, past `EndDate` → `Status = Completed`, `NextRunDate = null`, done.
   - Recurring, still active → `NextRunDate = CalculateNextRunDate(now, Frequency)` (`AddDays(7)`/`AddMonths(1)`/`AddYears(1)`), **then, unconditionally, `foreach (var assignment in schedule.Assignments) { assignment.IsProcessed = false; assignment.ProcessedAt = null; }`** ([:195-200](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L195-L200)) — every assignment on the schedule, not just the ones just processed.
6. `SaveChangesAsync`.

**This command is dual-purpose and both callers hit the same code** — its own doc
comment says so: *"Called by both manual action and background job"*
([ProcessToolboxTalkScheduleCommand.cs:8](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommand.cs#L8)). The manual path is a real, admin-facing "Process Now" action — `POST /api/toolbox-talks/schedules/{id}/process`, wired to `processToolboxTalkSchedule()` in [web/src/lib/api/toolbox-talks/schedules.ts:93-96](../web/src/lib/api/toolbox-talks/schedules.ts#L93-L96) and used from both the schedule detail page ([schedules/[id]/page.tsx](../web/src/app/(authenticated)/admin/toolbox-talks/schedules/[id]/page.tsx)) and the schedule list ([ScheduleList.tsx](../web/src/features/toolbox-talks/components/ScheduleList.tsx)). Any fix must account for admins being able to trigger this same defect manually, independent of the cron job.

### A.3 — Relationship between "processing" and "refreshing" — not actually separated

The commit that introduced department/site targeting explicitly intended them
to be reconciled together on the same cadence:

> "Recurring schedules re-derive criteria-based membership on refresh via an
> `IsCriteriaDerived` flag, so department/location membership stays current
> without disturbing explicitly-added employees." — commit `56cc5bd`, *Add
> department and location targeting to learning schedules*

But in the implementation, "refresh" was wired as a **fallback branch inside
the same `Handle` call that also does "process this cycle"** — gated on "zero
unprocessed assignments" as a proxy for "we're at the start of a new cycle."
That proxy condition is never actually reachable in the real lifecycle (§A.2
step 5 resets it every time), so in practice there is only ever one code path
that runs: "process whatever is currently unprocessed." The refresh path is
dead code under real cron/manual invocation.

---

## B. Confirmed at runtime, not just by reading

A temporary integration test
(`ReconRecurringRefreshReachabilityTests.SecondProcessCall_OnRecurringDepartmentSchedule_SkipsRefreshAndDuplicatesTalks`)
was added to `tests/QuantumBuild.Tests.Integration/ToolboxTalks/`, run against a
real Postgres Testcontainers-backed API host (the project's standard integration
test harness — [CustomWebApplicationFactory.cs](../tests/QuantumBuild.Tests.Integration/Fixtures/CustomWebApplicationFactory.cs)), then deleted (no code changes retained). It:

1. Created a `Weekly`, department-targeted schedule with one member (`memberA`) via `POST /api/toolbox-talks/schedules`.
2. Called `POST /{id}/process` once (simulating the job's day-0 run) → `TalksCreated = 1`, `NextRunDate` advanced ~7 days out.
3. Added a **new** employee (`memberB`) to the targeted department — the exact scenario the refresh is meant to pick up.
4. Read the schedule row directly from the DB and confirmed `ScheduledDate` was unchanged (still the creation date) while `NextRunDate` had moved a week out — i.e. the job's `OR` filter would select this schedule as "due" again on the very next calendar day, 6 days before it was actually supposed to run.
5. Called `POST /{id}/process` again immediately (simulating that next-day job run).

**Result — test passed, confirming all three assertions:**
- `secondResult.TalksCreated == 1` — the handler reprocessed `memberA` (whose assignment had been reset to unprocessed at the end of step 2), producing a **second, duplicate `ScheduledTalk`** for the same employee/talk/schedule less than a week after the first.
- `memberB` was **not** added to the schedule's assignments — `RefreshAssignmentsForTargetCriteria` did not run, because `unprocessedAssignments.Any()` was `true` (from `memberA`'s reset), so the refresh gate never opened.
- A direct DB count confirmed **two** `ScheduledTalk` rows exist for `{memberA, talk}` after only two calls, one week apart in intent but back-to-back in reality.

```
Passed QuantumBuild.Tests.Integration.ToolboxTalks.ReconRecurringRefreshReachabilityTests
      .SecondProcessCall_OnRecurringDepartmentSchedule_SkipsRefreshAndDuplicatesTalks [1 s]
Test Run Successful. Total tests: 1. Passed: 1.
```

**Definitive answers:**
- Does the refresh fire in normal operation? **No — never**, for either `AssignToAllEmployees` or department/site-targeted recurring schedules. The guard condition it depends on cannot occur through the normal create → process → process… cycle (confirmed both by the test above and independently by the comment already left in `ScheduleTargetingUpdateAndRefreshTests.cs:165-169`, which had to invent a `MarkAllAssignmentsProcessedAsync` DB-write helper specifically because "this state is not reliably reachable via the normal two-step HTTP create+process flow").
- Is a schedule processed more often than its frequency intends? **Yes.** Once `ScheduledDate` is in the past (true for essentially every schedule beyond its first day), the job's due-filter selects it every single day it runs, and the handler has no cadence check of its own — it reprocesses every currently-unprocessed (i.e. always, thanks to the end-of-run reset) assignment on every invocation.

---

## C. Intended behaviour and concrete impact

### Intent

Confirmed by the commit message quoted in §A.3 and by the `IsCriteriaDerived`
doc comment on the entity ([ToolboxTalkScheduleAssignment.cs:32-36](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalkScheduleAssignment.cs#L32-L36)): a recurring schedule's membership (all-employees or department/site-derived) is *supposed* to stay current across cycles — new hires/transfers-in should be picked up, departed/transferred-out criteria-derived employees should drop off — without disturbing explicitly-added employees. This is a real, stated design goal, not an accepted "snapshot membership at creation time forever" choice. **The refresh being unreachable is a genuine defect relative to stated intent, not acceptable-by-design behaviour.**

### Impact if left unfixed

1. **Membership silently goes stale (the originally-suspected impact).** A recurring `AssignToAllEmployees` schedule never assigns the training to anyone hired after the schedule was created. A recurring department/site-targeted schedule never picks up a new hire or an internal transfer into the targeted department/site, and never drops someone who transferred out or went inactive — they keep receiving the recurring training indefinitely. No error, no log warning — this fails silently and would only be noticed by an admin manually comparing headcount to assignment counts.
2. **Worse, newly-confirmed impact: every employee already on a recurring schedule gets re-issued the talk (and a duplicate assignment email) every single day the daily job runs, not on the configured weekly/monthly/annual cadence.** For a schedule with N assigned employees, this is N new `ScheduledTalk` rows and up to N assignment emails **per day** instead of per cycle. There is no DB-level uniqueness constraint on `{EmployeeId, ToolboxTalkId, ScheduleId}` on `ScheduledTalk` (confirmed — [ScheduledTalkConfiguration.cs:137-161](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Persistence/Configurations/ScheduledTalkConfiguration.cs#L137-L161) lists only non-unique indexes) to catch this, so it fails silently at the data layer too. This is the more severe and more surprising finding — it should be independently checked against real production data (e.g. counting `ScheduledTalk` rows grouped by `{EmployeeId, ScheduleId}` for any `Active`, non-`Once` schedule) before treating it as confirmed-and-live-in-production, since something this visible (duplicate pending trainings, duplicate emails) would normally be expected to have already generated support complaints. It is unambiguously confirmed as the code's *actual behaviour* when invoked (§B); whether it has already caused visible symptoms in a live tenant is a separate, unverified question this recon did not have access to check.

---

## D. Fix surface (not designed — mapped only)

Two distinct problems need addressing, and they interact:

1. **The job's due-filter should not treat a stale `ScheduledDate` as perpetually "due."** Once `NextRunDate` exists (it's set at creation and after every non-completing run), it should probably be the sole authority for recurring schedules — `ScheduledDate` alone should likely only matter for the *first* run of a brand-new schedule (`NextRunDate` not yet meaningfully advanced) or `Once` schedules. Care needed: `NextRunDate` is nullable and is explicitly set to `null` on completion ([:170,188](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L170)) — but completed schedules are already excluded by the `Status == Active` filter, so that's not a live interaction risk. **Riskiest interaction:** the very first run of a schedule created with `ScheduledDate` in the future relies on `ScheduledDate.Date <= today` — `NextRunDate` is set equal to `ScheduledDate` at creation ([CreateToolboxTalkScheduleCommandHandler.cs:182](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L182)) so `NextRunDate <= today` alone would still correctly gate the first run — but this needs explicit verification against `Once` schedules and the `UpdateToolboxTalkScheduleCommandHandler` path (which also writes `ScheduledDate` on an admin edit, and its interaction with an already-set `NextRunDate` was not traced in this recon — flagging as unverified).
2. **The command handler processes whatever is unprocessed with no cadence check of its own**, trusting the caller completely. Since the same command is deliberately shared between the cron job and the admin's manual "Process Now" button (§A.2), a fix that adds a "not due yet" check *inside* the handler must decide what manual-trigger semantics should be: does "Process Now" on a recurring schedule mean "run it right now regardless of `NextRunDate`" (current behaviour, arguably intentional for the manual path) or should it also respect the cadence? **This is a product decision, not just a bug fix**, and conflating the two callers' intended semantics is the biggest risk of a naive fix.
3. **The "reset to unprocessed at end of run" step (§A.2 step 5) and the "refresh gate" (§A.2 step 3) are load-bearing for each other in a way a fix must not break.** The reset exists so the *next* cycle has something to process; the gate exists to detect "nothing left to process, must be a new cycle, refresh membership first." A fix likely needs to separate "is a new cycle actually starting" (a real date/cadence check) from "are there zero unprocessed assignments" (an incidental, currently-always-true-after-reset condition) — these are conflated today and that conflation is the root cause. Whatever replaces the gate must still guarantee the refresh runs *before* the per-assignment processing loop on a legitimate new cycle (so newly-qualifying employees get created `ScheduledTalk`s in the same run they're added — this ordering is presumably intentional today, confirmed by the refresh call sitting before the `unprocessedAssignments` re-read at [:83-84](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L83-L84) and [:92-93](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L92-L93)).
4. **Explicitly-added (non-criteria-derived) assignments must continue to be untouched by any refresh**, and criteria-derived removal must continue to use the existing `ExecuteDeleteAsync` + `Detach` pattern (there's a specific EF orphan-removal/soft-delete-interceptor landmine documented inline at [:255-258](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L255-L258) and [:313-316](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L313-L316) — a naive rewrite that reintroduces plain `Remove()` without the `Detach` would silently break, producing 0-row UPDATEs instead of deletes). Not part of the reachability bug itself, but adjacent code a fix will necessarily touch.
5. **`Once` schedules are unaffected and must stay that way** — they complete after their single run and are excluded from the `Frequency != Once` guards on both refresh branches; no interaction found, but worth a regression test since any due-filter change touches the same query all schedule types flow through.

## E. Existing test coverage of this path

| File | Covers | Gap |
|---|---|---|
| [ScheduleTargetingTests.cs](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/ScheduleTargetingTests.cs) (7 tests) | Create-time department/site resolution only | No process/refresh coverage at all |
| [ScheduleTargetingUpdateAndRefreshTests.cs](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/ScheduleTargetingUpdateAndRefreshTests.cs) | Update-handler resolution (3 tests) + `RefreshAssignmentsForTargetCriteria` logic in isolation (2 tests) | The 2 refresh tests **force** the unreachable precondition via a direct DB write (`MarkAllAssignmentsProcessedAsync`, [:311-329](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/ScheduleTargetingUpdateAndRefreshTests.cs#L311-L329)) — they prove the refresh method is *correct* when it runs, not that it *ever runs naturally*. The file's own comment ([:165-169](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/ScheduleTargetingUpdateAndRefreshTests.cs#L165-L169)) already flags this. |
| [SchedulingTests.cs](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/SchedulingTests.cs) | Create/process/update for explicit-employee-list schedules only | No `TargetDepartmentIds`/`TargetSiteIds` anywhere in this file (grep-confirmed). `ProcessSchedule_RecurringWeekly_SetsNextRunDate` only asserts on the *first* `/process` call's result — it never calls `/process` a second time, so it can't observe the duplicate-processing behaviour in §B. |
| `ProcessToolboxTalkSchedulesJob` (the job itself, its due-filter, its tenant loop) | **Zero tests** (grep-confirmed, no matches anywhere under `tests/`) | The `ScheduledDate OR NextRunDate` filter that drives §B's finding has no test coverage of any kind — unit or integration. |

**No regression net exists for either half of the confirmed defect** (dead
refresh branch, over-frequent reprocessing). A fix should add, at minimum: (a)
a characterisation test equivalent to the temporary one in §B, made permanent,
covering both the all-employees and department/site refresh variants across two
consecutive `/process` calls without any forced DB write; (b) a direct test of
`ProcessToolboxTalkSchedulesJob`'s due-selection query against `Active` schedules
with a past `ScheduledDate` and a future `NextRunDate`, to lock in whatever the
corrected selection semantics become; (c) a `Once`-schedule regression test
proving the fix doesn't change one-time completion behaviour.
