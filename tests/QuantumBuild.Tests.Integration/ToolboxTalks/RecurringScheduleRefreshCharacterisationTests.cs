using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Services;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Characterisation tests for the recurring-schedule refresh/reprocessing defects documented in
/// docs/recurring-refresh-reachability-recon.md. Every test here captures CURRENT (broken)
/// behaviour and PASSES against today's unfixed code — this is the verified baseline the Chunk 2
/// fix will be measured against. No production code is changed in this chunk.
///
/// Two independent, compounding defects, both captured below:
///
///  - DEFECT 1 (refresh never fires): the criteria/all-employees refresh block
///    (RefreshAssignmentsForTargetCriteria / RefreshAssignmentsForAllEmployees in
///    ProcessToolboxTalkScheduleCommandHandler) is gated on "zero unprocessed assignments" — a
///    state that never occurs naturally, because every non-completing recurring run resets ALL
///    assignments back to unprocessed at the end of that same run. So the refresh never fires in
///    normal operation and membership goes stale.
///
///  - DEFECT 2 (reprocessed every job run, not on cadence): ScheduledDate is set once at creation
///    and never advanced. The daily job's due-filter ORs `ScheduledDate.Date <= today` with
///    `NextRunDate <= today`, so once ScheduledDate is in the past (true from day one onward) a
///    schedule is selected as "due" every single day the job runs, regardless of
///    NextRunDate/frequency. Combined with defect 1 (assignments always reset to unprocessed),
///    this reprocesses every assignee and creates a duplicate ScheduledTalk (and would send a
///    duplicate assignment email) on every run instead of once per cadence interval.
///
/// The command handler (ProcessToolboxTalkScheduleCommandHandler) is shared between the cron job
/// (ProcessToolboxTalkSchedulesJob) and the admin "Process Now" button — both entry points hit the
/// exact same code, so the manual button is equally affected by defect 1. Defect 2's due-filter,
/// however, is only evaluated by the job itself (the manual button always processes on demand
/// regardless of due date) — the last test below exercises the job directly for that reason.
///
/// SEPARATE PRE-EXISTING GAP (found while writing the last test, not one of the two defects
/// above, and NOT fixed here): ProcessToolboxTalkSchedulesJob never sets
/// IJobTenantContextAccessor.TenantId before calling _mediator.Send(...), unlike e.g.
/// BulkSopImportJob.cs:206. Without an HttpContext (exactly how Hangfire's cron invokes it),
/// ICurrentUserService.TenantId falls back to Guid.Empty, so ApplicationDbContext's global tenant
/// query filter makes ProcessToolboxTalkScheduleCommandHandler's schedule lookup match nothing —
/// every schedule, in every tenant, throws "not found" (caught per-schedule and merely logged).
/// Confirmed empirically: the real job cannot successfully process ANY schedule when invoked
/// exactly as Hangfire would. The last test below works around this in-test only (sets
/// IJobTenantContextAccessor.TenantId on the job's own scope before calling ExecuteAsync, so the
/// due-filter defect can be observed in isolation) — no production code is touched. The
/// IJobTenantContextAccessor gap itself is a distinct bug requiring its own fix, out of scope here.
/// </summary>
public class RecurringScheduleRefreshCharacterisationTests : IntegrationTestBase
{
    public RecurringScheduleRefreshCharacterisationTests(CustomWebApplicationFactory factory) : base(factory) { }

    #region DEFECT 2 — Daily reprocessing produces duplicates

    [Fact]
    public async Task CurrentBehaviour_ProcessCalledTwiceBeforeCadenceInterval_CreatesDuplicateScheduledTalks()
    {
        // CAPTURES CURRENT BROKEN BEHAVIOUR — Chunk 2 will invert this assertion.
        //
        // A Weekly recurring schedule, processed twice back-to-back (same day — nowhere near the
        // 7-day cadence interval) currently creates a SECOND ScheduledTalk (and would send a
        // second assignment email) for the same employee, because the end-of-run reset in
        // ProcessToolboxTalkScheduleCommandHandler unconditionally marks all assignments
        // unprocessed again, regardless of whether a full cycle has actually elapsed.
        var talk = await CreateTestTalkAsync();
        var employee = TestTenantConstants.Employees.Employee1;

        var createCommand = new
        {
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddYears(1),
            Frequency = ToolboxTalkFrequency.Weekly,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { employee }
        };
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", createCommand);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<ScheduleResult>();

        // Act — simulate the job's day-0 run, then an immediate next run (well inside the 7-day
        // Weekly cadence — this should NOT reprocess anything if cadence were respected).
        var firstResponse = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{created!.Id}/process", null);
        var firstResult = await firstResponse.Content.ReadFromJsonAsync<ProcessResult>();

        var secondResponse = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{created.Id}/process", null);
        var secondResult = await secondResponse.Content.ReadFromJsonAsync<ProcessResult>();

        // Assert — CURRENT (wrong) outcome: both calls report a talk created, and two distinct
        // ScheduledTalk rows now exist for the same employee/schedule/talk pairing, a week apart
        // in intent but back-to-back in reality.
        firstResult!.TalksCreated.Should().Be(1);
        secondResult!.TalksCreated.Should().Be(1,
            "current broken behaviour: the second call reprocesses the same employee instead of " +
            "skipping until NextRunDate, because assignments were reset to unprocessed at the end " +
            "of the first run");

        var scheduledTalkCount = await CountScheduledTalksAsync(created.Id, employee);
        scheduledTalkCount.Should().Be(2,
            "current broken behaviour: duplicate ScheduledTalk rows (and duplicate assignment " +
            "emails) are created well inside the Weekly cadence interval");
    }

    #endregion

    #region DEFECT 1 — Refresh never fires, membership goes stale

    [Fact]
    public async Task CurrentBehaviour_NewDepartmentMemberAfterProcessing_IsNotPickedUpOnNextRun()
    {
        // CAPTURES CURRENT BROKEN BEHAVIOUR — Chunk 2 will invert this assertion.
        //
        // A recurring, department-targeted schedule is processed once, a new employee then joins
        // the targeted department, and the schedule is processed again on a later run — via the
        // real create -> process -> process flow, with NO forced DB write. The refresh
        // (RefreshAssignmentsForTargetCriteria) is supposed to pick up the new member, but its
        // guard (`if (!unprocessedAssignments.Any())`) never passes in normal flow, because the
        // first run's end-of-run reset already left memberA's assignment unprocessed again — so
        // the refresh branch is skipped entirely and memberB is never added.
        var department = await CreateDepartmentAsync("Recurring Refresh Recon Dept");
        var memberA = await CreateEmployeeAsync("ReconStale", "A", department);
        var talk = await CreateTestTalkAsync();

        var createCommand = new
        {
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddYears(1),
            Frequency = ToolboxTalkFrequency.Weekly,
            AssignToAllEmployees = false,
            TargetDepartmentIds = new[] { department }
        };
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", createCommand);
        var created = await createResponse.Content.ReadFromJsonAsync<ScheduleResult>();
        created!.AssignmentCount.Should().Be(1);

        // Simulate the job's day-0 run (no forced DB write — this is the real create->process flow).
        var firstResponse = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{created.Id}/process", null);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // A new employee joins the targeted department after the schedule was last processed —
        // exactly the scenario the refresh is meant to pick up on the next cycle.
        var memberB = await CreateEmployeeAsync("ReconStale", "B", department);

        // Act — simulate a later job run (e.g. next week's due run).
        var secondResponse = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{created.Id}/process", null);

        // Assert — CURRENT (wrong) outcome: memberB is NOT added to the schedule's assignments,
        // because unprocessedAssignments.Any() was true (memberA was reset to unprocessed at the
        // end of the first run), so the refresh gate never opened.
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondResult = await secondResponse.Content.ReadFromJsonAsync<ProcessResult>();
        secondResult!.TalksCreated.Should().Be(1,
            "memberA is reprocessed (defect 2) while memberB is never added (defect 1)");

        var assignedEmployeeIds = await GetScheduleAssignmentEmployeeIdsAsync(created.Id);
        assignedEmployeeIds.Should().NotContain(memberB,
            "current broken behaviour: the refresh guard never passes in normal flow, so a new " +
            "department member is silently missed");
        assignedEmployeeIds.Should().BeEquivalentTo(new[] { memberA });
    }

    [Fact]
    public async Task CurrentBehaviour_EndOfRunReset_MarksAllAssignmentsUnprocessed_WhichIsWhyRefreshNeverFires()
    {
        // CAPTURES CURRENT BROKEN BEHAVIOUR — Chunk 2 will invert this assertion.
        //
        // Documents the mechanism behind defect 1: after any non-completing recurring run, every
        // assignment on the schedule (not just the ones just processed) is reset to
        // IsProcessed = false. This is exactly why `if (!unprocessedAssignments.Any())` can never
        // pass naturally on the next call — there is always at least one unprocessed assignment
        // going in, so the refresh gate can never see "a new cycle is starting".
        var talk = await CreateTestTalkAsync();
        var employee = TestTenantConstants.Employees.Employee1;

        var createCommand = new
        {
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddYears(1),
            Frequency = ToolboxTalkFrequency.Weekly,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { employee }
        };
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", createCommand);
        var created = await createResponse.Content.ReadFromJsonAsync<ScheduleResult>();

        // Act
        var response = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{created!.Id}/process", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — CURRENT (wrong) outcome: the assignment is unprocessed again immediately after
        // a single successful, non-completing run — not "unprocessed once the next cadence date
        // arrives", just unconditionally unprocessed.
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IToolboxTalksDbContext>();
        var assignment = await dbContext.ToolboxTalkScheduleAssignments
            .IgnoreQueryFilters()
            .Where(a => a.ScheduleId == created.Id)
            .SingleAsync();

        assignment.IsProcessed.Should().BeFalse(
            "current broken behaviour: the end-of-run reset unconditionally clears IsProcessed on " +
            "every assignment, which is the reason the refresh guard can never observe zero " +
            "unprocessed assignments in normal operation");
    }

    #endregion

    #region DEFECT 2 — Stale ScheduledDate keeps the job's due-filter perpetually true

    [Fact]
    public async Task CurrentBehaviour_JobDueFilter_SelectsScheduleWithFutureNextRunDate_BecauseScheduledDateNeverAdvances()
    {
        // CAPTURES CURRENT BROKEN BEHAVIOUR — Chunk 2 will invert this assertion.
        //
        // ProcessToolboxTalkSchedulesJob's due-filter is:
        //   ScheduledDate.Date <= today || (NextRunDate.HasValue && NextRunDate.Value.Date <= today)
        // ScheduledDate is set once at creation and never advanced by the handler. After one
        // process run on a Weekly schedule, NextRunDate is correctly pushed ~7 days out (not due
        // yet) — but ScheduledDate is still today's date, so the OR is still true and the job's own
        // due-selection query picks the schedule up again well before NextRunDate.
        //
        // This exercises the actual ProcessToolboxTalkSchedulesJob (not the admin /process
        // endpoint, which always processes on demand regardless of due date) so the job's real
        // due-filter query is what gets proven wrong here — the same query the daily Hangfire cron
        // runs.
        var talk = await CreateTestTalkAsync();
        var employee = TestTenantConstants.Employees.Employee1;

        var createCommand = new
        {
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddYears(1),
            Frequency = ToolboxTalkFrequency.Weekly,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { employee }
        };
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", createCommand);
        var created = await createResponse.Content.ReadFromJsonAsync<ScheduleResult>();

        // First run — simulates the job's day-0 pass via the admin endpoint. Advances NextRunDate
        // ~7 days out.
        var firstResponse = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{created!.Id}/process", null);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        DateTime scheduledDateAfterFirstRun;
        DateTime? nextRunDateAfterFirstRun;
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IToolboxTalksDbContext>();
            var scheduleRow = await dbContext.ToolboxTalkSchedules
                .IgnoreQueryFilters()
                .SingleAsync(s => s.Id == created.Id);
            scheduledDateAfterFirstRun = scheduleRow.ScheduledDate;
            nextRunDateAfterFirstRun = scheduleRow.NextRunDate;
        }

        // Confirm the precondition this test relies on: NextRunDate has moved into the future
        // (not due by cadence), but ScheduledDate is unchanged (still today, i.e. <= today).
        nextRunDateAfterFirstRun.Should().NotBeNull();
        nextRunDateAfterFirstRun!.Value.Date.Should().BeAfter(DateTime.UtcNow.Date,
            "NextRunDate correctly advanced ~7 days out for a Weekly schedule");
        scheduledDateAfterFirstRun.Date.Should().BeOnOrBefore(DateTime.UtcNow.Date,
            "ScheduledDate is write-once at creation and is never advanced by the handler");

        // Act — run the actual ProcessToolboxTalkSchedulesJob (not the /process endpoint), so the
        // job's own due-filter query is exercised for real, exactly as the daily Hangfire cron
        // would invoke it. Resolved via ActivatorUtilities (as Hangfire's own job activator does)
        // since the job class is not itself registered in DI.
        //
        // Test-only workaround for a SEPARATE, pre-existing gap (see class doc comment): the job
        // never sets IJobTenantContextAccessor.TenantId itself, so without an HttpContext the
        // command handler's tenant-scoped schedule lookup matches nothing and every schedule
        // errors with "not found" (caught per-schedule inside the job and merely logged) — the job
        // cannot successfully process ANY schedule as currently written when invoked exactly as
        // Hangfire's cron would invoke it. Setting the accessor here simulates what a correctly
        // wired job would do, so this test can isolate and prove the due-filter defect on its own.
        // No production code is changed by this workaround.
        using (var jobScope = Factory.Services.CreateScope())
        {
            jobScope.ServiceProvider.GetRequiredService<IJobTenantContextAccessor>().TenantId = TestTenantConstants.TenantId;
            var job = ActivatorUtilities.CreateInstance<ProcessToolboxTalkSchedulesJob>(jobScope.ServiceProvider);
            await job.ExecuteAsync(CancellationToken.None);
        }

        // Assert — CURRENT (wrong) outcome: the job treated the schedule as due (via the stale
        // ScheduledDate half of the OR) despite NextRunDate being ~7 days in the future, and
        // reprocessed it, producing a second ScheduledTalk for the same employee.
        var scheduledTalkCount = await CountScheduledTalksAsync(created.Id, employee);
        scheduledTalkCount.Should().Be(2,
            "current broken behaviour: the job's due-filter ORs in the stale ScheduledDate, so it " +
            "selects the schedule as due and reprocesses it even though NextRunDate says it is not " +
            "due for another ~7 days");
    }

    #endregion

    #region Helper Methods

    private async Task<Guid> CreateDepartmentAsync(string name)
    {
        var command = new { Name = $"{name} {Guid.NewGuid():N}", Code = (string?)null, IsActive = true };
        var response = await AdminClient.PostAsJsonAsync("/api/departments", command);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<MinimalResult<IdOnly>>();
        return result!.Data!.Id;
    }

    private async Task<Guid> CreateEmployeeAsync(string firstName, string lastName, Guid departmentId)
    {
        var command = new
        {
            FirstName = firstName,
            LastName = lastName,
            DepartmentId = departmentId,
            IsActive = true,
            CreateUserAccount = false
        };
        var response = await AdminClient.PostAsJsonAsync("/api/employees", command);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<MinimalResult<IdOnly>>();
        return result!.Data!.Id;
    }

    private async Task<Guid> CreateTestTalkAsync()
    {
        var createCommand = new
        {
            Title = $"Recurring Refresh Recon Talk {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdOnly>();
        return result!.Id;
    }

    private async Task<HashSet<Guid>> GetScheduleAssignmentEmployeeIdsAsync(Guid scheduleId)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IToolboxTalksDbContext>();
        return (await dbContext.ToolboxTalkScheduleAssignments
            .IgnoreQueryFilters()
            .Where(a => a.ScheduleId == scheduleId)
            .Select(a => a.EmployeeId)
            .ToListAsync()).ToHashSet();
    }

    private async Task<int> CountScheduledTalksAsync(Guid scheduleId, Guid employeeId)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IToolboxTalksDbContext>();
        return await dbContext.ScheduledTalks
            .IgnoreQueryFilters()
            .Where(t => t.ScheduleId == scheduleId && t.EmployeeId == employeeId)
            .CountAsync();
    }

    #endregion

    #region Response DTOs

    private record MinimalResult<T>(bool Success, T? Data);

    private record IdOnly(Guid Id);

    private record ScheduleResult(Guid Id, bool AssignToAllEmployees, int AssignmentCount, List<ScheduleAssignmentResult> Assignments);

    private record ScheduleAssignmentResult(Guid EmployeeId);

    private record ProcessResult(Guid ScheduleId, int TalksCreated, bool ScheduleCompleted, DateTime? NextRunDate);

    #endregion
}
