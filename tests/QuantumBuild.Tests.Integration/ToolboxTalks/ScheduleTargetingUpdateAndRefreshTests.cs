using System.Net;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Characterisation tests for the department/site targeting expansion on the UPDATE and
/// recurring-REFRESH paths (Learning Targeting Chunk 2a). <see cref="ScheduleTargetingTests"/>
/// covers only the CREATE path — these tests close that gap so the Chunk 2a shared-resolver
/// extraction has a safety net proving behaviour is unchanged before and after.
/// </summary>
public class ScheduleTargetingUpdateAndRefreshTests : IntegrationTestBase
{
    public ScheduleTargetingUpdateAndRefreshTests(CustomWebApplicationFactory factory) : base(factory) { }

    #region Update Handler — Department/Site Expansion

    [Fact]
    public async Task UpdateSchedule_TargetDepartment_ResolvesToExactlyActiveDepartmentMembers()
    {
        // Arrange — schedule starts targeting an unrelated explicit employee, then is updated
        // to target a department instead. The resolved assignment set must be replaced entirely.
        var department = await CreateDepartmentAsync("Update Targeting Dept A");
        var memberA = await CreateEmployeeAsync("UpdMember", "A", departmentId: department);
        var memberB = await CreateEmployeeAsync("UpdMember", "B", departmentId: department);
        var inactiveMember = await CreateEmployeeAsync("UpdInactive", "Member", departmentId: department, isActive: false);
        var outsider = await CreateEmployeeAsync("UpdOutsider", "Person");
        var talk = await CreateTestTalkAsync();

        var createCommand = new
        {
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { outsider }
        };
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", createCommand);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<ScheduleResult>();

        var updateCommand = new
        {
            Id = created!.Id,
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            TargetDepartmentIds = new[] { department }
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync($"/api/toolbox-talks/schedules/{created.Id}", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ScheduleResult>();
        var assignedIds = updated!.Assignments.Select(a => a.EmployeeId).ToHashSet();
        assignedIds.Should().BeEquivalentTo(new[] { memberA, memberB });
        assignedIds.Should().NotContain(inactiveMember);
        assignedIds.Should().NotContain(outsider);
    }

    [Fact]
    public async Task UpdateSchedule_DepartmentAndSiteTargets_UnionDeduplicated()
    {
        // Arrange
        var department = await CreateDepartmentAsync("Update Union Dept");
        var site = await CreateSiteAsync("Update Union Site");
        var deptOnly = await CreateEmployeeAsync("UpdDept", "Only", departmentId: department);
        var siteOnly = await CreateEmployeeAsync("UpdSite", "Only", siteId: site);
        var both = await CreateEmployeeAsync("UpdBoth", "Targets", departmentId: department, siteId: site);
        var talk = await CreateTestTalkAsync();

        var createCommand = new
        {
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            TargetDepartmentIds = new[] { department }
        };
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", createCommand);
        var created = await createResponse.Content.ReadFromJsonAsync<ScheduleResult>();

        var updateCommand = new
        {
            Id = created!.Id,
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            TargetDepartmentIds = new[] { department },
            TargetSiteIds = new[] { site }
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync($"/api/toolbox-talks/schedules/{created.Id}", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ScheduleResult>();
        updated!.AssignmentCount.Should().Be(3);
        var assignedIds = updated.Assignments.Select(a => a.EmployeeId).ToHashSet();
        assignedIds.Should().BeEquivalentTo(new[] { deptOnly, siteOnly, both });
    }

    [Fact]
    public async Task UpdateSchedule_SupervisorTargetsDepartment_OnlyReachesAssignedOperators()
    {
        // Arrange — a supervisor updating a schedule to target a department only reaches the
        // operators assigned to them, even though the department contains other employees.
        var department = await CreateDepartmentAsync("Update Supervisor Dept");
        var assignedOperator = await CreateEmployeeAsync("UpdAssigned", "Operator", departmentId: department);
        var unassignedButInDept = await CreateEmployeeAsync("UpdUnassigned", "InDept", departmentId: department);
        var talk = await CreateTestTalkAsync();

        var assignResponse = await AdminClient.PostAsJsonAsync(
            $"/api/employees/{QuantumBuild.Tests.Common.TestTenant.TestTenantConstants.Employees.SupervisorEmployee}/operators",
            new { OperatorEmployeeIds = new[] { assignedOperator } });
        assignResponse.EnsureSuccessStatusCode();

        var createCommand = new
        {
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { assignedOperator }
        };
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", createCommand);
        var created = await createResponse.Content.ReadFromJsonAsync<ScheduleResult>();

        var updateCommand = new
        {
            Id = created!.Id,
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            TargetDepartmentIds = new[] { department }
        };

        // Act
        var response = await SupervisorClient.PutAsJsonAsync($"/api/toolbox-talks/schedules/{created.Id}", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ScheduleResult>();
        var assignedIds = updated!.Assignments.Select(a => a.EmployeeId).ToHashSet();
        assignedIds.Should().BeEquivalentTo(new[] { assignedOperator });
        assignedIds.Should().NotContain(unassignedButInDept);
    }

    #endregion

    #region Process/Refresh Handler — RefreshAssignmentsForTargetCriteria

    [Fact]
    public async Task ProcessSchedule_RecurringTargetCriteria_AddsNewlyQualifyingEmployee()
    {
        // Arrange — a recurring schedule targeted by department. The refresh now runs
        // unconditionally on every /process call (Chunk 2b), so the "zero unprocessed
        // assignments" precondition this test used to force directly via the DB (see
        // docs/targeting-expansion-extraction-recon.md §7) is no longer needed or valid — a
        // real first /process call establishes the schedule's first cycle (and its
        // LastProcessedCycleDate marker) the same way the cron would.
        var department = await CreateDepartmentAsync("Refresh Add Dept");
        var memberA = await CreateEmployeeAsync("RefreshAdd", "A", departmentId: department);
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

        // Establishes the schedule's first cycle for real (processes memberA).
        var firstResponse = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{created.Id}/process", null);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // A new employee joins the targeted department after the schedule was last processed
        var memberB = await CreateEmployeeAsync("RefreshAdd", "B", departmentId: department);

        // Act
        var response = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{created.Id}/process", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ProcessResult>();
        result!.TalksCreated.Should().Be(1); // only memberB was unprocessed going into this run

        var assignments = await GetScheduleAssignmentEmployeeIdsAsync(created.Id);
        assignments.Should().BeEquivalentTo(new[] { memberA, memberB });
    }

    [Fact]
    public async Task ProcessSchedule_RecurringTargetCriteria_RemovesNoLongerQualifying_PreservesExplicit()
    {
        // Arrange — a recurring schedule with one criteria-derived (department) member and one
        // explicitly-added member. The department member moves out of the department; the
        // refresh must remove only the criteria-derived assignment, leaving the explicit one
        // untouched even though it never matched the department criteria to begin with.
        var department = await CreateDepartmentAsync("Refresh Remove Dept");
        var criteriaMember = await CreateEmployeeAsync("RefreshRemove", "Criteria", departmentId: department);
        var explicitMember = await CreateEmployeeAsync("RefreshRemove", "Explicit");
        var talk = await CreateTestTalkAsync();

        var createCommand = new
        {
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddYears(1),
            Frequency = ToolboxTalkFrequency.Weekly,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { explicitMember },
            TargetDepartmentIds = new[] { department }
        };
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", createCommand);
        var created = await createResponse.Content.ReadFromJsonAsync<ScheduleResult>();
        created!.AssignmentCount.Should().Be(2);

        // Establishes the schedule's first cycle for real (processes both members).
        var firstResponse = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{created.Id}/process", null);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // criteriaMember moves out of the targeted department — no longer qualifies
        await RemoveEmployeeFromDepartmentAsync(criteriaMember);

        // Act
        var response = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{created.Id}/process", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ProcessResult>();
        result!.TalksCreated.Should().Be(0); // explicitMember was already processed; nothing new qualifies

        var assignments = await GetScheduleAssignmentEmployeeIdsAsync(created.Id);
        assignments.Should().BeEquivalentTo(new[] { explicitMember });
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

    private async Task<Guid> CreateSiteAsync(string name)
    {
        var command = new { SiteName = $"{name} {Guid.NewGuid():N}", IsActive = true };
        var response = await AdminClient.PostAsJsonAsync("/api/sites", command);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<MinimalResult<IdOnly>>();
        return result!.Data!.Id;
    }

    private async Task<Guid> CreateEmployeeAsync(
        string firstName,
        string lastName,
        Guid? departmentId = null,
        Guid? siteId = null,
        bool isActive = true)
    {
        var command = new
        {
            FirstName = firstName,
            LastName = lastName,
            DepartmentId = departmentId,
            PrimarySiteId = siteId,
            IsActive = isActive,
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
            Title = $"Test Talk for Targeting {Guid.NewGuid()}",
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
        var dbContext = GetService<IToolboxTalksDbContext>();
        return (await dbContext.ToolboxTalkScheduleAssignments
            .IgnoreQueryFilters()
            .Where(a => a.ScheduleId == scheduleId)
            .Select(a => a.EmployeeId)
            .ToListAsync()).ToHashSet();
    }

    private async Task RemoveEmployeeFromDepartmentAsync(Guid employeeId)
    {
        var coreDbContext = GetService<ICoreDbContext>();
        var employee = await coreDbContext.Employees.IgnoreQueryFilters().FirstAsync(e => e.Id == employeeId);
        employee.DepartmentId = null;
        await coreDbContext.SaveChangesAsync(CancellationToken.None);
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
