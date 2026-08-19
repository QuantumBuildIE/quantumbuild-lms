using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// First full-flow test built on the reusable real-dispatch capability (see
/// Fixtures/HangfireDispatchWebApplicationFactory.cs, Fixtures/HangfireDispatchTestCollection.cs,
/// Fixtures/HangfireDispatchTestHelpers.cs).
///
/// RecurringScheduleRefreshCharacterisationTests already proves the recurring-schedule cron's business
/// logic (Chunk 2a tenant-context fix, Chunk 2b cadence/idempotency/refresh fixes) is correct, but does
/// so by resolving ProcessToolboxTalkSchedulesJob directly via ActivatorUtilities and calling
/// ExecuteAsync — which never proves Hangfire's own dispatch (enqueue -> storage -> worker pickup)
/// actually reaches the job. This class proves the same established behaviours through the REAL
/// dispatch path instead: BackgroundJob.Enqueue&lt;ProcessToolboxTalkSchedulesJob&gt;, picked up by a
/// real AddHangfireServer() worker, polled to completion via HangfireDispatchTestHelpers.
/// </summary>
[Collection("HangfireDispatch")]
public class RecurringScheduleRealDispatchTests : IntegrationTestBase
{
    public RecurringScheduleRealDispatchTests(HangfireDispatchWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task RealDispatch_ProcessesDueSchedule_CreatesScheduledTalkAdvancesCadenceAndDoesNotDuplicateOnRepeatRun()
    {
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

        // Act — real dispatch: enqueue exactly as the daily cron does (see Program.cs's
        // recurringJobManager.AddOrUpdate<ProcessToolboxTalkSchedulesJob> registration), and let the
        // real Hangfire server pick it up and run it. No ActivatorUtilities, no direct ExecuteAsync call.
        await HangfireDispatchTestHelpers.EnqueueAndAwaitSuccessAsync<ProcessToolboxTalkSchedulesJob>(
            job => job.ExecuteAsync(CancellationToken.None));

        // Assert — dispatch reached the handler (2a) and created the ScheduledTalk.
        (await CountScheduledTalksAsync(created!.Id, employee)).Should().Be(1,
            "the real Hangfire dispatch reached ProcessToolboxTalkScheduleCommandHandler and created " +
            "the ScheduledTalk for the assigned employee");

        // Assert — cadence advanced (2b): a scheduled (cron) run pushes NextRunDate out by the
        // frequency interval.
        var nextRunDateAfterFirstRun = await GetScheduleNextRunDateAsync(created.Id);
        nextRunDateAfterFirstRun.Should().NotBeNull();
        nextRunDateAfterFirstRun!.Value.Date.Should().BeAfter(DateTime.UtcNow.Date,
            "a real cron dispatch run advances NextRunDate by the Weekly interval, same as the " +
            "direct-invocation characterisation test proves");

        // Act — a second real dispatch run, same day, well inside the Weekly cadence window.
        await HangfireDispatchTestHelpers.EnqueueAndAwaitSuccessAsync<ProcessToolboxTalkSchedulesJob>(
            job => job.ExecuteAsync(CancellationToken.None));

        // Assert — no duplicate. NextRunDate is now in the future, so the job's own due-filter
        // excludes the schedule on this second real dispatch run; nothing further is created.
        (await CountScheduledTalksAsync(created.Id, employee)).Should().Be(1,
            "a second real dispatch run before the next cadence interval must not duplicate the " +
            "ScheduledTalk — the due-filter (NextRunDate <= today) excludes the schedule until its " +
            "next cycle is actually due");
    }

    [Fact]
    public async Task RealDispatch_NewDepartmentMemberAddedAfterFirstCycle_IsPickedUpOnTheNextCycle()
    {
        var department = await CreateDepartmentAsync("Real Dispatch Refresh Dept");
        var memberA = await CreateEmployeeAsync("RealDispatch", "A", department);
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

        // Act — first real dispatch run (cycle 1).
        await HangfireDispatchTestHelpers.EnqueueAndAwaitSuccessAsync<ProcessToolboxTalkSchedulesJob>(
            job => job.ExecuteAsync(CancellationToken.None));

        (await CountScheduledTalksAsync(created.Id, memberA)).Should().Be(1,
            "cycle 1's real dispatch run creates the ScheduledTalk for the department's existing member");

        // A new employee joins the targeted department after cycle 1 was processed.
        var memberB = await CreateEmployeeAsync("RealDispatch", "B", department);

        // Simulate arriving at the next cadence interval (tests cannot wait 7 real days). Cycle 1 was
        // processed "today", so LastProcessedCycleDate already equals today's date — use a distinct
        // past due date so this unambiguously represents "the next interval's due date has arrived",
        // mirroring the direct-invocation characterisation test's approach.
        await SetScheduleNextRunDateAsync(created.Id, DateTime.UtcNow.Date.AddDays(-1));

        // Act — second real dispatch run (cycle 2).
        await HangfireDispatchTestHelpers.EnqueueAndAwaitSuccessAsync<ProcessToolboxTalkSchedulesJob>(
            job => job.ExecuteAsync(CancellationToken.None));

        // Assert — the membership refresh (which runs unconditionally on every processing call, per
        // Chunk 2b) picked up memberB via the real dispatch path, without any test-side workaround.
        var assignedEmployeeIds = await GetScheduleAssignmentEmployeeIdsAsync(created.Id);
        assignedEmployeeIds.Should().BeEquivalentTo(new[] { memberA, memberB },
            "the membership refresh fires on every real dispatch run, so the new department member " +
            "added after cycle 1 is present in the schedule's assignments after cycle 2's real run");

        (await CountScheduledTalksAsync(created.Id, memberB)).Should().Be(1,
            "the newly-added department member is processed on the next cycle's real dispatch run");

        (await CountScheduledTalksAsync(created.Id, memberA)).Should().Be(2,
            "the pre-existing member is reprocessed for cycle 2 as well, since a genuinely new cycle " +
            "became due");
    }

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
            Title = $"Real Dispatch Recon Talk {Guid.NewGuid()}",
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

    private async Task<DateTime?> GetScheduleNextRunDateAsync(Guid scheduleId)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IToolboxTalksDbContext>();
        var scheduleRow = await dbContext.ToolboxTalkSchedules
            .IgnoreQueryFilters()
            .SingleAsync(s => s.Id == scheduleId);
        return scheduleRow.NextRunDate;
    }

    /// <summary>
    /// Test-only DB manipulation to simulate a schedule's cadence reaching its next due date, since
    /// tests cannot wait real days between cycles. Mirrors
    /// RecurringScheduleRefreshCharacterisationTests.SetScheduleNextRunDateAsync.
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

    #endregion
}
