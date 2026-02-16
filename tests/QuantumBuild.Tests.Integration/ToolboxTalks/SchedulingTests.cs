using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for Toolbox Talk scheduling operations.
/// </summary>
public class SchedulingTests : IntegrationTestBase
{
    public SchedulingTests(CustomWebApplicationFactory factory) : base(factory) { }

    #region Create Schedule Tests

    [Fact]
    public async Task CreateSchedule_ForSpecificEmployees_CreatesAssignments()
    {
        // Arrange - First create a talk
        var talk = await CreateTestTalkAsync();

        // Use UTC DateTime for PostgreSQL timestamptz compatibility
        var command = new
        {
            ToolboxTalkId = talk.Id,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            EmployeeIds = new[]
            {
                TestTenantConstants.Employees.Employee1,
                TestTenantConstants.Employees.Employee2
            },
            Notes = "Test schedule for specific employees"
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var schedule = await response.Content.ReadFromJsonAsync<ToolboxTalkScheduleDto>();
        schedule.Should().NotBeNull();
        schedule!.Id.Should().NotBeEmpty();
        schedule.ToolboxTalkId.Should().Be(talk.Id);
        schedule.AssignToAllEmployees.Should().BeFalse();
        schedule.Assignments.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateSchedule_ForAllEmployees_CreatesScheduleWithFlag()
    {
        // Arrange
        var talk = await CreateTestTalkAsync();

        // Use UTC DateTime for PostgreSQL timestamptz compatibility
        var command = new
        {
            ToolboxTalkId = talk.Id,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = true,
            Notes = "Test schedule for all employees"
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var schedule = await response.Content.ReadFromJsonAsync<ToolboxTalkScheduleDto>();
        schedule.Should().NotBeNull();
        schedule!.AssignToAllEmployees.Should().BeTrue();
    }

    [Fact]
    public async Task CreateSchedule_RecurringWeekly_SetsFrequencyAndEndDate()
    {
        // Arrange
        var talk = await CreateTestTalkAsync();

        // Use UTC DateTime for PostgreSQL timestamptz compatibility
        var command = new
        {
            ToolboxTalkId = talk.Id,
            ScheduledDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddMonths(3),
            Frequency = ToolboxTalkFrequency.Weekly,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { TestTenantConstants.Employees.Employee1 },
            Notes = "Weekly recurring schedule"
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var schedule = await response.Content.ReadFromJsonAsync<ToolboxTalkScheduleDto>();
        schedule.Should().NotBeNull();
        schedule!.Frequency.Should().Be(ToolboxTalkFrequency.Weekly);
        schedule.EndDate.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateSchedule_RecurringMonthly_SetsCorrectFrequency()
    {
        // Arrange
        var talk = await CreateTestTalkAsync();

        // Use UTC DateTime for PostgreSQL timestamptz compatibility
        var command = new
        {
            ToolboxTalkId = talk.Id,
            ScheduledDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddYears(1),
            Frequency = ToolboxTalkFrequency.Monthly,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { TestTenantConstants.Employees.Employee1 },
            Notes = "Monthly recurring schedule"
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var schedule = await response.Content.ReadFromJsonAsync<ToolboxTalkScheduleDto>();
        schedule!.Frequency.Should().Be(ToolboxTalkFrequency.Monthly);
    }

    [Fact]
    public async Task CreateSchedule_InvalidTalkId_ReturnsBadRequest()
    {
        // Arrange
        // Use UTC DateTime for PostgreSQL timestamptz compatibility
        var command = new
        {
            ToolboxTalkId = Guid.NewGuid(),
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = true
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSchedule_Unauthenticated_Returns401()
    {
        // Arrange - Use UTC DateTime for PostgreSQL timestamptz compatibility
        var command = new
        {
            ToolboxTalkId = Guid.NewGuid(),
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = true
        };

        // Act
        var response = await UnauthenticatedClient.PostAsJsonAsync("/api/toolbox-talks/schedules", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateSchedule_WithoutPermission_Returns403()
    {
        // Arrange - Use UTC DateTime for PostgreSQL timestamptz compatibility
        var command = new
        {
            ToolboxTalkId = Guid.NewGuid(),
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = true
        };

        // Act - Finance user doesn't have ToolboxTalks.Schedule permission
        var response = await FinanceClient.PostAsJsonAsync("/api/toolbox-talks/schedules", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Get Schedule Tests

    [Fact]
    public async Task GetSchedules_ReturnsPagedResults()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/toolbox-talks/schedules?pageNumber=1&pageSize=10");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<ToolboxTalkScheduleListDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.PageNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetScheduleById_ExistingSchedule_ReturnsSchedule()
    {
        // Arrange - Create a schedule
        var talk = await CreateTestTalkAsync();
        // Use UTC DateTime for PostgreSQL timestamptz compatibility
        var createCommand = new
        {
            ToolboxTalkId = talk.Id,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(1),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { TestTenantConstants.Employees.Employee1 }
        };
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", createCommand);
        var createdSchedule = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkScheduleDto>();

        // Act
        var response = await AdminClient.GetAsync($"/api/toolbox-talks/schedules/{createdSchedule!.Id}");
        var schedule = await response.Content.ReadFromJsonAsync<ToolboxTalkScheduleDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        schedule.Should().NotBeNull();
        schedule!.Id.Should().Be(createdSchedule.Id);
    }

    [Fact]
    public async Task GetScheduleById_NonExistingSchedule_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await AdminClient.GetAsync($"/api/toolbox-talks/schedules/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSchedules_FilterByStatus_ReturnsFilteredResults()
    {
        // Act
        var response = await AdminClient.GetAsync($"/api/toolbox-talks/schedules?status={ToolboxTalkScheduleStatus.Active}");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<ToolboxTalkScheduleListDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Success.Should().BeTrue();
    }

    #endregion

    #region Process Schedule Tests

    [Fact]
    public async Task ProcessSchedule_CreatesScheduledTalks()
    {
        // Arrange - Create a schedule
        var talk = await CreateTestTalkAsync();
        // Use UTC DateTime for PostgreSQL timestamptz compatibility
        var createCommand = new
        {
            ToolboxTalkId = talk.Id,
            ScheduledDate = DateTime.UtcNow.Date,
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { TestTenantConstants.Employees.Employee1 }
        };
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", createCommand);
        var schedule = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkScheduleDto>();

        // Act
        var response = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{schedule!.Id}/process", null);
        var result = await response.Content.ReadFromJsonAsync<ProcessScheduleResultDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.ScheduleId.Should().Be(schedule.Id);
        result.TalksCreated.Should().Be(1);
    }

    [Fact]
    public async Task ProcessSchedule_RecurringWeekly_SetsNextRunDate()
    {
        // Arrange
        var talk = await CreateTestTalkAsync();
        // Use UTC DateTime for PostgreSQL timestamptz compatibility
        var createCommand = new
        {
            ToolboxTalkId = talk.Id,
            ScheduledDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddMonths(3),
            Frequency = ToolboxTalkFrequency.Weekly,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { TestTenantConstants.Employees.Employee1 }
        };
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", createCommand);
        var schedule = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkScheduleDto>();

        // Act - Process first run
        var response = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{schedule!.Id}/process", null);
        var result = await response.Content.ReadFromJsonAsync<ProcessScheduleResultDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.NextRunDate.Should().NotBeNull();
        result.ScheduleCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessSchedule_OneTimeSchedule_CompletesSchedule()
    {
        // Arrange
        var talk = await CreateTestTalkAsync();
        // Use UTC DateTime for PostgreSQL timestamptz compatibility
        var createCommand = new
        {
            ToolboxTalkId = talk.Id,
            ScheduledDate = DateTime.UtcNow.Date,
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { TestTenantConstants.Employees.Employee1 }
        };
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", createCommand);
        var schedule = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkScheduleDto>();

        // Act
        var response = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{schedule!.Id}/process", null);
        var result = await response.Content.ReadFromJsonAsync<ProcessScheduleResultDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.ScheduleCompleted.Should().BeTrue();
        result.NextRunDate.Should().BeNull();
    }

    [Fact]
    public async Task ProcessSchedule_NonExistingSchedule_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{nonExistentId}/process", null);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    #endregion

    #region Cancel Schedule Tests

    [Fact]
    public async Task CancelSchedule_ActiveSchedule_ReturnsNoContent()
    {
        // Arrange - Create a schedule
        var talk = await CreateTestTalkAsync();
        // Use UTC DateTime for PostgreSQL timestamptz compatibility
        var createCommand = new
        {
            ToolboxTalkId = talk.Id,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(7),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { TestTenantConstants.Employees.Employee1 }
        };
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", createCommand);
        var schedule = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkScheduleDto>();

        // Act
        var response = await AdminClient.DeleteAsync($"/api/toolbox-talks/schedules/{schedule!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CancelSchedule_NonExistingSchedule_ReturnsBadRequest()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await AdminClient.DeleteAsync($"/api/toolbox-talks/schedules/{nonExistentId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    #endregion

    #region Update Schedule Tests

    [Fact]
    public async Task UpdateSchedule_ModifyNotes_ReturnsOk()
    {
        // Arrange - Create a schedule
        var talk = await CreateTestTalkAsync();
        // Use UTC DateTime for PostgreSQL timestamptz compatibility
        var createCommand = new
        {
            ToolboxTalkId = talk.Id,
            ScheduledDate = DateTime.UtcNow.Date.AddDays(7),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { TestTenantConstants.Employees.Employee1 },
            Notes = "Original notes"
        };
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", createCommand);
        var schedule = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkScheduleDto>();

        // Update - Use DateTime directly (will be serialized to UTC)
        var updateCommand = new
        {
            Id = schedule!.Id,
            ToolboxTalkId = talk.Id,
            ScheduledDate = schedule.ScheduledDate,
            Frequency = schedule.Frequency,
            AssignToAllEmployees = schedule.AssignToAllEmployees,
            EmployeeIds = new[] { TestTenantConstants.Employees.Employee1 },
            Notes = "Updated notes"
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync($"/api/toolbox-talks/schedules/{schedule.Id}", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ToolboxTalkScheduleDto>();
        updated!.Notes.Should().Be("Updated notes");
    }

    #endregion

    #region Helper Methods

    private async Task<ToolboxTalkDto> CreateTestTalkAsync()
    {
        var createCommand = new
        {
            Title = $"Test Talk for Schedule {Guid.NewGuid()}",
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
        return (await response.Content.ReadFromJsonAsync<ToolboxTalkDto>())!;
    }

    #endregion

    #region Response DTOs

    private record ResultWrapper<T>(
        bool Success,
        T? Data,
        string? Message,
        List<string>? Errors
    );

    private record PaginatedResult<T>(
        List<T> Items,
        int PageNumber,
        int PageSize,
        int TotalCount,
        int TotalPages
    );

    private record ToolboxTalkDto(
        Guid Id,
        string Title,
        string? Description,
        ToolboxTalkFrequency Frequency,
        bool RequiresQuiz,
        bool IsActive,
        List<object> Sections,
        DateTime CreatedAt
    );

    private record ToolboxTalkScheduleDto(
        Guid Id,
        Guid ToolboxTalkId,
        string ToolboxTalkTitle,
        DateTime ScheduledDate,
        DateTime? EndDate,
        ToolboxTalkFrequency Frequency,
        bool AssignToAllEmployees,
        ToolboxTalkScheduleStatus Status,
        DateTime? NextRunDate,
        string? Notes,
        int AssignmentCount,
        List<ToolboxTalkScheduleAssignmentDto> Assignments,
        DateTime CreatedAt
    );

    private record ToolboxTalkScheduleListDto(
        Guid Id,
        Guid ToolboxTalkId,
        string ToolboxTalkTitle,
        DateTime ScheduledDate,
        ToolboxTalkFrequency Frequency,
        bool AssignToAllEmployees,
        ToolboxTalkScheduleStatus Status,
        int AssignmentCount,
        DateTime CreatedAt
    );

    private record ToolboxTalkScheduleAssignmentDto(
        Guid Id,
        Guid EmployeeId,
        string EmployeeName
    );

    private record ProcessScheduleResultDto(
        Guid ScheduleId,
        int TalksCreated,
        bool ScheduleCompleted,
        DateTime? NextRunDate,
        string Message
    );

    #endregion
}
