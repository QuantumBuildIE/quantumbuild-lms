namespace QuantumBuild.Tests.Integration.Core;

/// <summary>
/// Integration tests for the single-supervisor-per-operator uniqueness invariant (§3.15 item H).
/// </summary>
public class SupervisorAssignmentTests : IntegrationTestBase
{
    public SupervisorAssignmentTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1 — Assigning an operator to a second supervisor returns Conflict
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AssignOperator_WhenAlreadyAssignedToAnotherSupervisor_ReturnsConflict()
    {
        // Arrange — create two supervisors and one operator as plain employees
        var supA = await CreateTestEmployeeAsync("SUPA");
        var supB = await CreateTestEmployeeAsync("SUPB");
        var op   = await CreateTestEmployeeAsync("OPTR");

        // Assign Op to SupA — should succeed
        var firstAssign = await AssignOperatorAsync(supA, op);
        firstAssign.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act — attempt to assign the same Op to SupB
        var secondAssign = await AssignOperatorAsync(supB, op);

        // Assert
        secondAssign.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await secondAssign.Content.ReadFromJsonAsync<ResultWrapper<object>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e == "This operator is already assigned to another supervisor. Unassign them first before reassigning.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2 — Restore-on-reassign still works after unassignment
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AssignOperator_AfterUnassignment_RestoresOriginalAssignment()
    {
        // Arrange
        var sup = await CreateTestEmployeeAsync("SUP1");
        var op  = await CreateTestEmployeeAsync("OPR1");

        // Initial assign — should succeed
        var assignResponse = await AssignOperatorAsync(sup, op);
        assignResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Get the assignment ID so we can verify the same row is restored
        var assignResult = await assignResponse.Content.ReadFromJsonAsync<ResultWrapper<List<AssignmentDto>>>();
        var originalAssignmentId = assignResult!.Data!.Single().Id;

        // Unassign
        var unassign = await AdminClient.DeleteAsync($"/api/employees/{sup}/operators/{op}");
        unassign.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act — re-assign to the SAME supervisor
        var reassignResponse = await AssignOperatorAsync(sup, op);

        // Assert — success
        reassignResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the restored row has the same ID (restore-on-reassign, not new insert)
        var reassignResult = await reassignResponse.Content.ReadFromJsonAsync<ResultWrapper<List<AssignmentDto>>>();
        reassignResult!.Data!.Single().Id.Should().Be(originalAssignmentId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3 — Assigning to a different supervisor after unassignment succeeds
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AssignOperator_ToDifferentSupervisorAfterUnassignment_Succeeds()
    {
        // Arrange
        var supA = await CreateTestEmployeeAsync("SUA2");
        var supB = await CreateTestEmployeeAsync("SUB2");
        var op   = await CreateTestEmployeeAsync("OPR2");

        // Assign Op to SupA then unassign
        var firstAssign = await AssignOperatorAsync(supA, op);
        firstAssign.StatusCode.Should().Be(HttpStatusCode.OK);

        var unassign = await AdminClient.DeleteAsync($"/api/employees/{supA}/operators/{op}");
        unassign.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act — assign Op to SupB (no active assignment exists anywhere)
        var secondAssign = await AssignOperatorAsync(supB, op);

        // Assert — succeeds, not blocked by the soft-deleted SupA→Op row
        secondAssign.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await secondAssign.Content.ReadFromJsonAsync<ResultWrapper<List<AssignmentDto>>>();
        result!.Success.Should().BeTrue();
        result.Data!.Should().ContainSingle(a => a.OperatorEmployeeId == op);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 4 — Available-operators query excludes already-supervised operators
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableOperators_ExcludesOperatorsAlreadySupervisedByOthers()
    {
        // Arrange
        var supA = await CreateTestEmployeeAsync("SUA3");
        var supB = await CreateTestEmployeeAsync("SUB3");
        var op   = await CreateTestEmployeeAsync("OPR3");

        // Assign Op to SupA
        var assign = await AssignOperatorAsync(supA, op);
        assign.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act — get available operators for SupB
        var response = await AdminClient.GetAsync($"/api/employees/{supB}/operators/available");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<List<OperatorDto>>>();
        result!.Success.Should().BeTrue();
        result.Data!.Should().NotContain(o => o.EmployeeId == op,
            "operator already assigned to SupA must not appear as available for SupB");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<Guid> CreateTestEmployeeAsync(string codePrefix)
    {
        var unique = Guid.NewGuid().ToString("N")[..6];
        var command = new
        {
            EmployeeCode = $"{codePrefix}-{unique}",
            FirstName = codePrefix,
            LastName = "Test",
            IsActive = true
        };

        var response = await AdminClient.PostAsJsonAsync("/api/employees", command);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<EmployeeDto>>();
        return result!.Data!.Id;
    }

    private Task<HttpResponseMessage> AssignOperatorAsync(Guid supervisorId, Guid operatorId)
    {
        var body = new { operatorEmployeeIds = new[] { operatorId } };
        return AdminClient.PostAsJsonAsync($"/api/employees/{supervisorId}/operators", body);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Response DTOs (private to this test class)
    // ─────────────────────────────────────────────────────────────────────────

    private record ResultWrapper<T>(
        bool Success,
        T? Data,
        string? Message,
        List<string>? Errors
    );

    private record EmployeeDto(Guid Id, string EmployeeCode, string FirstName, string LastName);

    private record AssignmentDto(
        Guid Id,
        Guid SupervisorEmployeeId,
        string SupervisorName,
        Guid OperatorEmployeeId,
        string OperatorName,
        DateTime AssignedAt,
        string AssignedBy
    );

    private record OperatorDto(
        Guid EmployeeId,
        string EmployeeCode,
        string FullName,
        string? Department,
        string? JobTitle
    );
}
