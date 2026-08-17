using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Verifies the recurring-schedule processing lifecycle fixed in Chunk 2b, against the defects
/// documented in docs/recurring-refresh-reachability-recon.md and captured (as broken behaviour)
/// by the original version of this test class.
///
/// The two defects that were fixed in ProcessToolboxTalkScheduleCommandHandler:
///
///  - DEFECT 1 (refresh never fired): the criteria/all-employees refresh is no longer gated on
///    "zero unprocessed assignments" (a state that could never occur naturally). It now runs
///    unconditionally, every time a recurring schedule is processed, on both the cron path and
///    the manual "Process Now" path.
///
///  - DEFECT 2 (reprocessed every job run, not on cadence): ProcessToolboxTalkSchedulesJob's
///    due-filter no longer ORs in the write-once ScheduledDate; a schedule is due only when
///    NextRunDate <= today. ProcessToolboxTalkScheduleCommand gained an IsScheduledRun flag — only
///    a scheduled (cron) run advances NextRunDate/cadence; a manual run processes on demand without
///    disturbing it. Assignments are no longer unconditionally reset to unprocessed at the end of
///    every run; instead ToolboxTalkSchedule.LastProcessedCycleDate tracks which due date was last
///    handled, so assignments are only reopened once a genuinely new cycle becomes due — this
///    makes repeat calls against the same due date (cron after manual, or a repeat click)
///    idempotent instead of duplicating ScheduledTalks/emails.
///
/// The command handler (ProcessToolboxTalkScheduleCommandHandler) is shared between the cron job
/// (ProcessToolboxTalkSchedulesJob) and the admin "Process Now" button — both entry points hit the
/// exact same code, differing only in the IsScheduledRun flag the caller passes.
///
/// TENANT CONTEXT (Chunk 2a, fixed): ProcessToolboxTalkSchedulesJob sets
/// IJobTenantContextAccessor.TenantId per schedule in a fresh DI scope before dispatching
/// ProcessToolboxTalkScheduleCommand, mirroring BulkSopImportJob's fresh-scope-per-item pattern
/// (see ProcessToolboxTalkSchedulesJob.ProcessScheduleInFreshScopeAsync). See
/// ExecuteAsync_NoManualTenantContextWorkaround_ProcessesDueScheduleAndCreatesScheduledTalk below
/// for the before/after proof, and docs/schedule-job-tenant-context-recon.md for the original recon.
/// </summary>
public class RecurringScheduleRefreshCharacterisationTests : IntegrationTestBase
{
    public RecurringScheduleRefreshCharacterisationTests(CustomWebApplicationFactory factory) : base(factory) { }

    #region Idempotency — repeat processing within the same cycle does not duplicate

    [Fact]
    public async Task ProcessCalledTwiceBeforeCadenceInterval_DoesNotCreateDuplicateScheduledTalks()
    {
        // FIXED BEHAVIOUR (was: CurrentBehaviour_ProcessCalledTwiceBeforeCadenceInterval_CreatesDuplicateScheduledTalks).
        //
        // A Weekly recurring schedule, processed twice back-to-back via the manual "Process Now"
        // endpoint (same day — nowhere near the 7-day cadence interval), must create the
        // ScheduledTalk only once. The manual endpoint never advances NextRunDate, and
        // LastProcessedCycleDate prevents the second call from reopening the already-processed
        // assignment.
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

        // Act — two manual calls back-to-back, well inside the 7-day Weekly cadence.
        var firstResponse = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{created!.Id}/process", null);
        var firstResult = await firstResponse.Content.ReadFromJsonAsync<ProcessResult>();

        var secondResponse = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{created.Id}/process", null);
        var secondResult = await secondResponse.Content.ReadFromJsonAsync<ProcessResult>();

        // Assert — first call processes the employee; the second is a no-op (idempotent).
        firstResult!.TalksCreated.Should().Be(1);
        secondResult!.TalksCreated.Should().Be(0,
            "the second call falls within the same cycle as the first (LastProcessedCycleDate " +
            "matches NextRunDate), so the already-processed assignment is not reopened");

        var scheduledTalkCount = await CountScheduledTalksAsync(created.Id, employee);
        scheduledTalkCount.Should().Be(1,
            "no duplicate ScheduledTalk (or duplicate assignment email) is created within the " +
            "Weekly cadence interval");
    }

    #endregion

    #region TENANT CONTEXT (Chunk 2a): cron now reaches the handler

    [Fact]
    public async Task ExecuteAsync_NoManualTenantContextWorkaround_ProcessesDueScheduleAndCreatesScheduledTalk()
    {
        // Proves the Chunk 2a tenant-context fix. ProcessToolboxTalkSchedulesJob is constructed and
        // invoked exactly as Hangfire's own cron activator would: via ActivatorUtilities, with no
        // HttpContext and no manual IJobTenantContextAccessor pre-set anywhere.
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

        // Act. Run the actual job, resolved via ActivatorUtilities (mirroring Hangfire's own job
        // activator), with NO manual tenant-context workaround anywhere in this test.
        using (var jobScope = Factory.Services.CreateScope())
        {
            var job = ActivatorUtilities.CreateInstance<ProcessToolboxTalkSchedulesJob>(jobScope.ServiceProvider);
            await job.ExecuteAsync(CancellationToken.None);
        }

        // Assert. The cron reaches ProcessToolboxTalkScheduleCommandHandler and creates a
        // ScheduledTalk for the assigned employee.
        var scheduledTalkCount = await CountScheduledTalksAsync(created!.Id, employee);
        scheduledTalkCount.Should().Be(1,
            "the job sets IJobTenantContextAccessor.TenantId per schedule in a fresh scope, so " +
            "the cron reaches the command handler and creates the ScheduledTalk");
    }

    #endregion

    #region Membership refresh — fires on every processing run, cron and manual alike

    [Fact]
    public async Task NewDepartmentMemberAfterProcessing_IsPickedUpOnNextRun()
    {
        // FIXED BEHAVIOUR (was: CurrentBehaviour_NewDepartmentMemberAfterProcessing_IsNotPickedUpOnNextRun).
        //
        // A recurring, department-targeted schedule is processed once, a new employee then joins
        // the targeted department, and the schedule is processed again — via the real
        // create -> process -> process flow. The refresh now runs unconditionally on every
        // processing call, so the new member is picked up without needing a "zero unprocessed
        // assignments" state to occur first.
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

        // First processing run.
        var firstResponse = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{created.Id}/process", null);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // A new employee joins the targeted department after the schedule was last processed.
        var memberB = await CreateEmployeeAsync("ReconStale", "B", department);

        // Act — a later processing run (e.g. an admin clicking Process Now again, or the next
        // cron pass).
        var secondResponse = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{created.Id}/process", null);

        // Assert — memberB is added to the schedule's assignments and processed; memberA is not
        // reprocessed (already handled in the current cycle).
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondResult = await secondResponse.Content.ReadFromJsonAsync<ProcessResult>();
        secondResult!.TalksCreated.Should().Be(1,
            "memberA was already processed this cycle; only the newly-refreshed memberB is processed");

        var assignedEmployeeIds = await GetScheduleAssignmentEmployeeIdsAsync(created.Id);
        assignedEmployeeIds.Should().Contain(memberB,
            "the membership refresh now runs unconditionally on every processing call, so a new " +
            "department member is picked up immediately");
        assignedEmployeeIds.Should().BeEquivalentTo(new[] { memberA, memberB });
    }

    [Fact]
    public async Task ProcessedAssignment_StaysProcessed_UntilANewCycleBecomesDue()
    {
        // FIXED BEHAVIOUR (was: CurrentBehaviour_EndOfRunReset_MarksAllAssignmentsUnprocessed_WhichIsWhyRefreshNeverFires).
        //
        // After a single, non-completing recurring run, the assignment stays marked processed —
        // it is no longer unconditionally reset to unprocessed at the end of the run. It only
        // becomes unprocessed again once a genuinely new cycle becomes due.
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

        // Assert — the assignment remains processed immediately after a single successful,
        // non-completing run.
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IToolboxTalksDbContext>();
        var assignment = await dbContext.ToolboxTalkScheduleAssignments
            .IgnoreQueryFilters()
            .Where(a => a.ScheduleId == created.Id)
            .SingleAsync();

        assignment.IsProcessed.Should().BeTrue(
            "assignments are only reopened when a new cycle becomes due (NextRunDate advances " +
            "past LastProcessedCycleDate), not unconditionally at the end of every run");
    }

    #endregion

    #region Due-filter — cadence is respected, not perpetually re-triggered

    [Fact]
    public async Task JobDueFilter_DoesNotReselectSchedule_WhileNextRunDateIsInTheFuture()
    {
        // FIXED BEHAVIOUR (was: CurrentBehaviour_JobDueFilter_SelectsScheduleWithFutureNextRunDate_BecauseScheduledDateNeverAdvances).
        //
        // ProcessToolboxTalkSchedulesJob's due-filter is now purely NextRunDate <= today — the
        // stale-ScheduledDate half of the old OR is gone. The FIRST job run (a real cron
        // invocation, IsScheduledRun=true) processes the due cycle and advances NextRunDate ~7
        // days out; a SECOND job run immediately after must not reselect the schedule at all.
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

        // Act — first real job run. Due (NextRunDate == ScheduledDate == today).
        await RunScheduleJobAsync();

        DateTime? nextRunDateAfterFirstRun;
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IToolboxTalksDbContext>();
            var scheduleRow = await dbContext.ToolboxTalkSchedules
                .IgnoreQueryFilters()
                .SingleAsync(s => s.Id == created!.Id);
            nextRunDateAfterFirstRun = scheduleRow.NextRunDate;
        }

        nextRunDateAfterFirstRun.Should().NotBeNull();
        nextRunDateAfterFirstRun!.Value.Date.Should().BeAfter(DateTime.UtcNow.Date,
            "NextRunDate correctly advances ~7 days out for a Weekly schedule on a scheduled run");

        var scheduledTalkCountAfterFirstRun = await CountScheduledTalksAsync(created!.Id, employee);
        scheduledTalkCountAfterFirstRun.Should().Be(1);

        // Act — second job run, same day. NextRunDate is now in the future, so the due-filter
        // must exclude the schedule entirely.
        await RunScheduleJobAsync();

        // Assert — no second ScheduledTalk was created; the job never even reached the handler
        // for this schedule.
        var scheduledTalkCount = await CountScheduledTalksAsync(created.Id, employee);
        scheduledTalkCount.Should().Be(1,
            "the due-filter is purely NextRunDate <= today now, so a schedule whose NextRunDate " +
            "was just advanced into the future is not reselected until that date arrives");
    }

    #endregion

    #region Cadence advance — only a scheduled (cron) run advances NextRunDate

    [Fact]
    public async Task ManualProcessNow_DoesNotAdvanceCadence_ScheduledCronRunDoes()
    {
        // NEW BEHAVIOUR (Chunk 2b). A manual "Process Now" call processes on demand but leaves
        // NextRunDate untouched; only a scheduled (cron) run advances it by the frequency
        // interval.
        var talk = await CreateTestTalkAsync();

        // Schedule A — processed via the manual endpoint.
        var employeeA = TestTenantConstants.Employees.Employee1;
        var createManual = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", new
        {
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddYears(1),
            Frequency = ToolboxTalkFrequency.Weekly,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { employeeA }
        });
        var manualSchedule = await createManual.Content.ReadFromJsonAsync<ScheduleResult>();
        var originalNextRunDate = manualSchedule!.NextRunDate;

        var manualProcessResponse = await AdminClient.PostAsync(
            $"/api/toolbox-talks/schedules/{manualSchedule.Id}/process", null);
        var manualProcessResult = await manualProcessResponse.Content.ReadFromJsonAsync<ProcessResult>();

        manualProcessResult!.TalksCreated.Should().Be(1);
        manualProcessResult.NextRunDate.Should().Be(originalNextRunDate,
            "a manual Process Now run does not advance cadence");

        // Schedule B — processed via the real job (cron path).
        var employeeB = TestTenantConstants.Employees.Employee2;
        var createCron = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", new
        {
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddYears(1),
            Frequency = ToolboxTalkFrequency.Weekly,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { employeeB }
        });
        var cronSchedule = await createCron.Content.ReadFromJsonAsync<ScheduleResult>();

        await RunScheduleJobAsync();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IToolboxTalksDbContext>();
        var cronScheduleRow = await dbContext.ToolboxTalkSchedules
            .IgnoreQueryFilters()
            .SingleAsync(s => s.Id == cronSchedule!.Id);

        cronScheduleRow.NextRunDate.Should().NotBeNull();
        cronScheduleRow.NextRunDate!.Value.Date.Should().BeAfter(DateTime.UtcNow.Date,
            "a scheduled (cron) run advances NextRunDate by the frequency interval");
    }

    #endregion

    #region Manual-then-cron same cycle — no duplicate, cadence still advances

    [Fact]
    public async Task ManualProcessThenCronOnSameCycle_DoesNotDuplicate_ButStillAdvancesCadence()
    {
        // NEW BEHAVIOUR (Chunk 2b edge case). An admin clicks Process Now while the schedule is
        // due (processes, does not advance cadence). The cron then runs later the same day for
        // the same due cycle: it must not reprocess the already-handled employee, but it must
        // still advance NextRunDate — cadence advancement is the cron's responsibility regardless
        // of whether the cycle's work was already done by a manual click.
        var talk = await CreateTestTalkAsync();
        var employee = TestTenantConstants.Employees.Employee1;

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", new
        {
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddYears(1),
            Frequency = ToolboxTalkFrequency.Weekly,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { employee }
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ScheduleResult>();

        // Manual click while due.
        var manualResponse = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{created!.Id}/process", null);
        var manualResult = await manualResponse.Content.ReadFromJsonAsync<ProcessResult>();
        manualResult!.TalksCreated.Should().Be(1);

        // Cron runs later the same day, same due cycle (NextRunDate was not advanced by the manual call).
        await RunScheduleJobAsync();

        var scheduledTalkCount = await CountScheduledTalksAsync(created.Id, employee);
        scheduledTalkCount.Should().Be(1,
            "the cron finds nothing left unprocessed for this cycle, so it does not duplicate the " +
            "ScheduledTalk the manual click already created");

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IToolboxTalksDbContext>();
        var scheduleRow = await dbContext.ToolboxTalkSchedules
            .IgnoreQueryFilters()
            .SingleAsync(s => s.Id == created.Id);

        scheduleRow.NextRunDate.Should().NotBeNull();
        scheduleRow.NextRunDate!.Value.Date.Should().BeAfter(DateTime.UtcNow.Date,
            "the cron still advances cadence even though the cycle's work was already done manually");
    }

    #endregion

    #region Processing on cadence — the next cycle's work is created once NextRunDate is reached

    [Fact]
    public async Task JobRun_OneIntervalLater_CreatesNextCycleScheduledTalks()
    {
        // NEW BEHAVIOUR (Chunk 2b). Simulates arriving at the next cadence interval by advancing
        // NextRunDate directly in the database (tests cannot wait 7 real days). A job run before
        // that point creates nothing further; a job run once NextRunDate is reached creates the
        // next cycle's ScheduledTalk for the same employee.
        var talk = await CreateTestTalkAsync();
        var employee = TestTenantConstants.Employees.Employee1;

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", new
        {
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddYears(1),
            Frequency = ToolboxTalkFrequency.Weekly,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { employee }
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ScheduleResult>();

        // First cycle.
        await RunScheduleJobAsync();
        (await CountScheduledTalksAsync(created!.Id, employee)).Should().Be(1);

        // A run before the next interval creates nothing further.
        await RunScheduleJobAsync();
        (await CountScheduledTalksAsync(created.Id, employee)).Should().Be(1,
            "NextRunDate has not been reached yet, so the job's due-filter excludes the schedule");

        // Simulate arriving at the next cadence interval. The schedule was created (and its
        // first cycle processed) "today", so LastProcessedCycleDate already equals today's date —
        // setting NextRunDate back to exactly today would collide with that marker and look like
        // the same cycle. Use a distinct due date in the past to unambiguously represent "the
        // next interval's due date has now arrived".
        await SetScheduleNextRunDateAsync(created.Id, DateTime.UtcNow.Date.AddDays(-1));

        // Act — the job run that reaches the next due cycle.
        await RunScheduleJobAsync();

        // Assert — a second ScheduledTalk is created for the new cycle, and cadence advances again.
        (await CountScheduledTalksAsync(created.Id, employee)).Should().Be(2,
            "a new cycle became due, so the employee is reprocessed for the next occurrence");

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IToolboxTalksDbContext>();
        var scheduleRow = await dbContext.ToolboxTalkSchedules
            .IgnoreQueryFilters()
            .SingleAsync(s => s.Id == created.Id);

        scheduleRow.NextRunDate.Should().NotBeNull();
        scheduleRow.NextRunDate!.Value.Date.Should().BeAfter(DateTime.UtcNow.Date,
            "cadence advances again from the newly-processed cycle's due date");
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

    /// <summary>
    /// Runs the actual ProcessToolboxTalkSchedulesJob, resolved via ActivatorUtilities (mirroring
    /// Hangfire's own job activator), exactly as the daily cron would invoke it.
    /// </summary>
    private async Task RunScheduleJobAsync()
    {
        using var jobScope = Factory.Services.CreateScope();
        var job = ActivatorUtilities.CreateInstance<ProcessToolboxTalkSchedulesJob>(jobScope.ServiceProvider);
        await job.ExecuteAsync(CancellationToken.None);
    }

    /// <summary>
    /// Test-only DB manipulation to simulate a schedule's cadence reaching its next due date,
    /// since tests cannot wait real days between cycles.
    /// </summary>
    private async Task SetScheduleNextRunDateAsync(Guid scheduleId, DateTime nextRunDate)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IToolboxTalksDbContext>();
        var scheduleRow = await dbContext.ToolboxTalkSchedules
            .IgnoreQueryFilters()
            .SingleAsync(s => s.Id == scheduleId);
        scheduleRow.NextRunDate = DateTime.SpecifyKind(nextRunDate, DateTimeKind.Utc);
        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    #endregion

    #region Response DTOs

    private record MinimalResult<T>(bool Success, T? Data);

    private record IdOnly(Guid Id);

    private record ScheduleResult(Guid Id, bool AssignToAllEmployees, int AssignmentCount, DateTime? NextRunDate, List<ScheduleAssignmentResult> Assignments);

    private record ScheduleAssignmentResult(Guid EmployeeId);

    private record ProcessResult(Guid ScheduleId, int TalksCreated, bool ScheduleCompleted, DateTime? NextRunDate);

    #endregion
}
