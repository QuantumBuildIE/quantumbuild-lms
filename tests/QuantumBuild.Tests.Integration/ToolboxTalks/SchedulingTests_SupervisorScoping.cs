using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for supervisor schedule scoping (§3.14).
/// Verifies that Supervisors can only schedule training for their assigned operators.
/// </summary>
public class SchedulingTests_SupervisorScoping : IntegrationTestBase
{
    public SchedulingTests_SupervisorScoping(CustomWebApplicationFactory factory) : base(factory) { }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async Task<Guid> CreateTestTalkAsync()
    {
        var command = new
        {
            Title = $"Supervisor Scoping Talk {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", command);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ToolboxTalkApiResult>();
        return result!.Id;
    }

    private async Task AssignOperatorToSupervisorAsync(Guid operatorEmployeeId)
    {
        var supervisorId = TestTenantConstants.Employees.SupervisorEmployee;
        var body = new { operatorEmployeeIds = new[] { operatorEmployeeId } };
        var response = await AdminClient.PostAsJsonAsync(
            $"/api/employees/{supervisorId}/operators", body);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> PostScheduleAsSupervisorAsync(
        Guid talkId,
        bool assignToAll,
        Guid[]? employeeIds = null)
    {
        var command = new
        {
            ToolboxTalkId = talkId,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = assignToAll,
            EmployeeIds = employeeIds ?? Array.Empty<Guid>(),
        };
        return await SupervisorClient.PostAsJsonAsync("/api/toolbox-talks/schedules", command);
    }

    // -----------------------------------------------------------------------
    // Test 1: Supervisor schedules to assigned operator → 201 Created
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Supervisor_SchedulesToAssignedOperator_Returns201()
    {
        var talkId = await CreateTestTalkAsync();
        await AssignOperatorToSupervisorAsync(TestTenantConstants.Employees.Employee1);

        var response = await PostScheduleAsSupervisorAsync(
            talkId,
            assignToAll: false,
            employeeIds: new[] { TestTenantConstants.Employees.Employee1 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // -----------------------------------------------------------------------
    // Test 2: Supervisor schedules to non-operator employee → 400 Bad Request
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Supervisor_SchedulesToNonOperatorEmployee_Returns400WithCount()
    {
        var talkId = await CreateTestTalkAsync();
        // Assign Employee1 only — Employee2 is not in the team
        await AssignOperatorToSupervisorAsync(TestTenantConstants.Employees.Employee1);

        var response = await PostScheduleAsSupervisorAsync(
            talkId,
            assignToAll: false,
            employeeIds: new[] { TestTenantConstants.Employees.Employee2 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("1 selected employee(s) are not in your assigned team");
    }

    // -----------------------------------------------------------------------
    // Test 3: Supervisor uses AssignToAllEmployees=true → 400 Bad Request
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Supervisor_AssignToAllEmployees_Returns400()
    {
        var talkId = await CreateTestTalkAsync();
        await AssignOperatorToSupervisorAsync(TestTenantConstants.Employees.Employee1);

        var response = await PostScheduleAsSupervisorAsync(
            talkId,
            assignToAll: true);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Supervisors cannot schedule training for all employees");
    }

    // -----------------------------------------------------------------------
    // Test 4: Supervisor schedules mixed (some assigned, some not) → 400 with count
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Supervisor_SchedulesMixedEmployees_Returns400WithCorrectCount()
    {
        var talkId = await CreateTestTalkAsync();
        // Only Employee1 assigned — Employee2 and Employee3 are outsiders
        await AssignOperatorToSupervisorAsync(TestTenantConstants.Employees.Employee1);

        var response = await PostScheduleAsSupervisorAsync(
            talkId,
            assignToAll: false,
            employeeIds: new[]
            {
                TestTenantConstants.Employees.Employee1,
                TestTenantConstants.Employees.Employee2,
                TestTenantConstants.Employees.Employee3
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("2 selected employee(s) are not in your assigned team");
    }

    // -----------------------------------------------------------------------
    // Test 5: Admin schedules same employee IDs → 201 (unrestricted)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Admin_SchedulesSameEmployees_Returns201()
    {
        var talkId = await CreateTestTalkAsync();

        var command = new
        {
            ToolboxTalkId = talkId,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            EmployeeIds = new[]
            {
                TestTenantConstants.Employees.Employee1,
                TestTenantConstants.Employees.Employee2
            },
        };
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // -----------------------------------------------------------------------
    // Test 6: Admin uses AssignToAllEmployees=true → 201 (unrestricted)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Admin_AssignToAllEmployees_Returns201()
    {
        var talkId = await CreateTestTalkAsync();

        var command = new
        {
            ToolboxTalkId = talkId,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = true,
        };
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // -----------------------------------------------------------------------
    // Test 7: Supervisor with zero assigned operators schedules → 400
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Supervisor_WithZeroAssignedOperators_Returns400()
    {
        var talkId = await CreateTestTalkAsync();
        // No operator assigned to this supervisor

        var response = await PostScheduleAsSupervisorAsync(
            talkId,
            assignToAll: false,
            employeeIds: new[] { TestTenantConstants.Employees.Employee1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("1 selected employee(s) are not in your assigned team");
    }

    // -----------------------------------------------------------------------
    // Test 8: Supervisor updates schedule to add non-operator → 400 (Update handler)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Supervisor_UpdatesScheduleToAddNonOperator_Returns400()
    {
        var talkId = await CreateTestTalkAsync();
        await AssignOperatorToSupervisorAsync(TestTenantConstants.Employees.Employee1);

        // Supervisor creates a valid schedule first (assigned operator only)
        var createResponse = await PostScheduleAsSupervisorAsync(
            talkId,
            assignToAll: false,
            employeeIds: new[] { TestTenantConstants.Employees.Employee1 });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            "Setup: Supervisor should be able to create a schedule for assigned operators");

        var created = await createResponse.Content.ReadFromJsonAsync<ScheduleDto>();

        // Now try to update to include a non-operator
        var updateCommand = new
        {
            Id = created!.Id,
            ToolboxTalkId = talkId,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            EmployeeIds = new[]
            {
                TestTenantConstants.Employees.Employee1,
                TestTenantConstants.Employees.Employee2 // Not in team
            },
        };
        var updateResponse = await SupervisorClient.PutAsJsonAsync(
            $"/api/toolbox-talks/schedules/{created.Id}", updateCommand);

        // Update action maps InvalidOperationException → 404 NotFound (see ToolboxTalkSchedulesController.Update)
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await updateResponse.Content.ReadAsStringAsync();
        body.Should().Contain("1 selected employee(s) are not in your assigned team");
    }

    // -----------------------------------------------------------------------
    // Private response DTOs
    // -----------------------------------------------------------------------

    private record ToolboxTalkApiResult(Guid Id, string Title);
    private record ScheduleDto(Guid Id, Guid ToolboxTalkId, bool AssignToAllEmployees);
}
