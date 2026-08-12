using System.Net;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for targeting a toolbox talk schedule by Department and/or Site
/// (Learning Targeting Chunk 1). Verifies the expansion to member employees is exact:
/// union of department + site + explicit employees, deduplicated, active-only, tenant-scoped.
/// </summary>
public class ScheduleTargetingTests : IntegrationTestBase
{
    public ScheduleTargetingTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task CreateSchedule_TargetDepartment_AssignsExactlyActiveDepartmentMembers()
    {
        // Arrange
        var department = await CreateDepartmentAsync("Targeting Dept A");
        var memberA = await CreateEmployeeAsync("Member", "A", departmentId: department);
        var memberB = await CreateEmployeeAsync("Member", "B", departmentId: department);
        var inactiveMember = await CreateEmployeeAsync("Inactive", "Member", departmentId: department, isActive: false);
        var outsider = await CreateEmployeeAsync("Outsider", "Person");
        var talk = await CreateTestTalkAsync();

        var command = new
        {
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            TargetDepartmentIds = new[] { department },
            Notes = "Targeted by department"
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var schedule = await response.Content.ReadFromJsonAsync<ScheduleResult>();
        schedule.Should().NotBeNull();
        var assignedIds = schedule!.Assignments.Select(a => a.EmployeeId).ToHashSet();
        assignedIds.Should().BeEquivalentTo(new[] { memberA, memberB });
        assignedIds.Should().NotContain(inactiveMember);
        assignedIds.Should().NotContain(outsider);
    }

    [Fact]
    public async Task CreateSchedule_TargetSite_AssignsExactlySiteMembers()
    {
        // Arrange
        var site = await CreateSiteAsync("Targeting Site A");
        var memberA = await CreateEmployeeAsync("Site", "A", siteId: site);
        var memberB = await CreateEmployeeAsync("Site", "B", siteId: site);
        var outsider = await CreateEmployeeAsync("Off", "Site");
        var talk = await CreateTestTalkAsync();

        var command = new
        {
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            TargetSiteIds = new[] { site }
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var schedule = await response.Content.ReadFromJsonAsync<ScheduleResult>();
        var assignedIds = schedule!.Assignments.Select(a => a.EmployeeId).ToHashSet();
        assignedIds.Should().BeEquivalentTo(new[] { memberA, memberB });
        assignedIds.Should().NotContain(outsider);
    }

    [Fact]
    public async Task CreateSchedule_DepartmentAndSiteTargets_UnionDeduplicated()
    {
        // Arrange
        var department = await CreateDepartmentAsync("Union Dept");
        var site = await CreateSiteAsync("Union Site");
        var deptOnly = await CreateEmployeeAsync("Dept", "Only", departmentId: department);
        var siteOnly = await CreateEmployeeAsync("Site", "Only", siteId: site);
        // In both the targeted department AND the targeted site — must appear exactly once
        var both = await CreateEmployeeAsync("Both", "Targets", departmentId: department, siteId: site);
        var talk = await CreateTestTalkAsync();

        var command = new
        {
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            TargetDepartmentIds = new[] { department },
            TargetSiteIds = new[] { site }
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var schedule = await response.Content.ReadFromJsonAsync<ScheduleResult>();
        schedule!.AssignmentCount.Should().Be(3);
        var assignedIds = schedule.Assignments.Select(a => a.EmployeeId).ToHashSet();
        assignedIds.Should().BeEquivalentTo(new[] { deptOnly, siteOnly, both });
    }

    [Fact]
    public async Task CreateSchedule_ExplicitEmployeePlusDepartmentTarget_UnionDeduplicated()
    {
        // Arrange
        var department = await CreateDepartmentAsync("Explicit Union Dept");
        var deptMember = await CreateEmployeeAsync("Dept", "Member", departmentId: department);
        var explicitEmployee = await CreateEmployeeAsync("Explicit", "Pick");
        var talk = await CreateTestTalkAsync();

        var command = new
        {
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            // deptMember selected both explicitly AND via the department target — must not duplicate
            EmployeeIds = new[] { explicitEmployee, deptMember },
            TargetDepartmentIds = new[] { department }
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var schedule = await response.Content.ReadFromJsonAsync<ScheduleResult>();
        schedule!.AssignmentCount.Should().Be(2);
        var assignedIds = schedule.Assignments.Select(a => a.EmployeeId).ToHashSet();
        assignedIds.Should().BeEquivalentTo(new[] { deptMember, explicitEmployee });
    }

    [Fact]
    public async Task CreateSchedule_AssignToAllEmployeesWithDepartmentTarget_IgnoresDepartmentAndAssignsEveryone()
    {
        // Arrange — AssignToAllEmployees overrides/ignores a department target per spec: "means
        // everyone... a department/location target is irrelevant/overridden - do not double-add."
        // NOTE: a FluentValidation rule also rejects this combination (mirroring the pre-existing
        // EmployeeIds-must-be-empty-when-AssignToAll rule), but this codebase's command validators
        // are registered in DI without being wired into either ASP.NET auto-validation or a MediatR
        // ValidationBehavior pipeline step (checked: no such behavior is registered anywhere), so no
        // FluentValidation validator for any MediatR command currently runs. That is a pre-existing,
        // cross-cutting gap outside this chunk's scope — flagged, not fixed here. The handler's own
        // branch logic is what actually governs behaviour, and it silently overrides, which is what
        // this test verifies.
        var department = await CreateDepartmentAsync("Overridden Dept");
        var deptMember = await CreateEmployeeAsync("Dept", "Member", departmentId: department);
        var talk = await CreateTestTalkAsync();

        var command = new
        {
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = true,
            TargetDepartmentIds = new[] { department }
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var schedule = await response.Content.ReadFromJsonAsync<ScheduleResult>();
        schedule!.AssignToAllEmployees.Should().BeTrue();
        // Everyone means everyone — including the department member, but not scoped to it
        schedule.Assignments.Select(a => a.EmployeeId).Should().Contain(deptMember);
        schedule.AssignmentCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task CreateSchedule_DepartmentFromAnotherTenant_ReturnsBadRequest()
    {
        // Arrange — a department GUID that doesn't belong to the caller's tenant must be rejected
        var foreignDepartmentId = Guid.NewGuid();
        var talk = await CreateTestTalkAsync();

        var command = new
        {
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            TargetDepartmentIds = new[] { foreignDepartmentId }
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSchedule_SupervisorTargetsDepartment_OnlyReachesAssignedOperators()
    {
        // Arrange — a supervisor targeting a department only reaches the operators assigned to
        // them, even though the department itself contains employees outside their team.
        var department = await CreateDepartmentAsync("Supervisor Scoped Dept");
        var assignedOperator = await CreateEmployeeAsync("Assigned", "Operator", departmentId: department);
        var unassignedButInDept = await CreateEmployeeAsync("Unassigned", "InDept", departmentId: department);
        var talk = await CreateTestTalkAsync();

        // Assign only `assignedOperator` to the test Supervisor (TestTenantConstants.Employees.SupervisorEmployee)
        var assignResponse = await AdminClient.PostAsJsonAsync(
            $"/api/employees/{QuantumBuild.Tests.Common.TestTenant.TestTenantConstants.Employees.SupervisorEmployee}/operators",
            new { OperatorEmployeeIds = new[] { assignedOperator } });
        assignResponse.EnsureSuccessStatusCode();

        var command = new
        {
            ToolboxTalkId = talk,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            TargetDepartmentIds = new[] { department }
        };

        // Act
        var response = await SupervisorClient.PostAsJsonAsync("/api/toolbox-talks/schedules", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var schedule = await response.Content.ReadFromJsonAsync<ScheduleResult>();
        var assignedIds = schedule!.Assignments.Select(a => a.EmployeeId).ToHashSet();
        assignedIds.Should().BeEquivalentTo(new[] { assignedOperator });
        assignedIds.Should().NotContain(unassignedButInDept);
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

    #endregion

    #region Response DTOs

    private record MinimalResult<T>(bool Success, T? Data);

    private record IdOnly(Guid Id);

    private record ScheduleResult(Guid Id, bool AssignToAllEmployees, int AssignmentCount, List<ScheduleAssignmentResult> Assignments);

    private record ScheduleAssignmentResult(Guid EmployeeId);

    #endregion
}
