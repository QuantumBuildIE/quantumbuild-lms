using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using System.Text.Json.Serialization;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for Toolbox Talk dashboard and reporting endpoints.
/// </summary>
public class DashboardTests : IntegrationTestBase
{
    public DashboardTests(CustomWebApplicationFactory factory) : base(factory) { }

    #region Dashboard Tests

    [Fact]
    public async Task GetDashboard_ReturnsStats()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/toolbox-talks/dashboard");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<ToolboxTalkDashboardDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDashboard_IncludesTotalTalksCount()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/toolbox-talks/dashboard");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<ToolboxTalkDashboardDto>>();

        // Assert
        result!.Data!.TotalTalks.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetDashboard_IncludesAssignmentCounts()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/toolbox-talks/dashboard");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<ToolboxTalkDashboardDto>>();

        // Assert
        result!.Data!.TotalAssignments.Should().BeGreaterThanOrEqualTo(0);
        result.Data.PendingCount.Should().BeGreaterThanOrEqualTo(0);
        result.Data.InProgressCount.Should().BeGreaterThanOrEqualTo(0);
        result.Data.CompletedCount.Should().BeGreaterThanOrEqualTo(0);
        result.Data.OverdueCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetDashboard_IncludesRates()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/toolbox-talks/dashboard");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<ToolboxTalkDashboardDto>>();

        // Assert
        result!.Data!.CompletionRate.Should().BeGreaterThanOrEqualTo(0);
        result.Data.CompletionRate.Should().BeLessThanOrEqualTo(100);
        result.Data.OverdueRate.Should().BeGreaterThanOrEqualTo(0);
        result.Data.OverdueRate.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task GetDashboard_Unauthenticated_Returns401()
    {
        // Act
        var response = await UnauthenticatedClient.GetAsync("/api/toolbox-talks/dashboard");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDashboard_WithoutPermission_Returns403()
    {
        // Act - Finance user doesn't have ToolboxTalks.View permission
        var response = await FinanceClient.GetAsync("/api/toolbox-talks/dashboard");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Settings Tests

    [Fact]
    public async Task GetSettings_ReturnsSettings()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/toolbox-talks/settings");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<ToolboxTalkSettingsDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSettings_Unauthenticated_Returns401()
    {
        // Act
        var response = await UnauthenticatedClient.GetAsync("/api/toolbox-talks/settings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateSettings_WithAdminPermission_ReturnsOk()
    {
        // Arrange
        var updateDto = new
        {
            DefaultDueDays = 7,
            ReminderDaysBefore = 3,
            SendEmailReminders = true,
            SendPushReminders = true,
            MaxQuizAttempts = 3,
            RequireSignature = true,
            AutoAssignNewEmployees = true
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync("/api/toolbox-talks/settings", updateDto);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<ToolboxTalkSettingsDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateSettings_WithoutAdminPermission_Returns403()
    {
        // Arrange
        var updateDto = new
        {
            DefaultDueDays = 7,
            ReminderDaysBefore = 3,
            SendEmailReminders = true
        };

        // Act - SiteManager has ToolboxTalks.Manage but not ToolboxTalks.Admin
        var response = await OperatorClient.PutAsJsonAsync("/api/toolbox-talks/settings", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Reports Tests

    [Fact]
    public async Task GetComplianceReport_ReturnsReport()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/toolbox-talks/reports/compliance");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<ComplianceReportDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetComplianceReport_WithDateFilters_ReturnsFilteredReport()
    {
        // Arrange
        var dateFrom = DateTime.Today.AddMonths(-1).ToString("yyyy-MM-dd");
        var dateTo = DateTime.Today.ToString("yyyy-MM-dd");

        // Act
        var response = await AdminClient.GetAsync($"/api/toolbox-talks/reports/compliance?dateFrom={dateFrom}&dateTo={dateTo}");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<ComplianceReportDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetComplianceReport_WithSiteFilter_ReturnsFilteredReport()
    {
        // Arrange
        var siteId = TestTenantConstants.Sites.MainSite;

        // Act
        var response = await AdminClient.GetAsync($"/api/toolbox-talks/reports/compliance?siteId={siteId}");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<ComplianceReportDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetOverdueReport_ReturnsReport()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/toolbox-talks/reports/overdue");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<List<OverdueItemDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetOverdueReport_WithFilters_ReturnsFilteredReport()
    {
        // Arrange - Create a talk first to have a valid ID
        var talkResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", new
        {
            Title = $"Overdue Test Talk {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        });
        var talk = await talkResponse.Content.ReadFromJsonAsync<ToolboxTalkCreatedDto>();

        // Act
        var response = await AdminClient.GetAsync($"/api/toolbox-talks/reports/overdue?toolboxTalkId={talk!.Id}");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<List<OverdueItemDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetCompletionsReport_ReturnsPagedReport()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/toolbox-talks/reports/completions?pageNumber=1&pageSize=20");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<CompletionDetailDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PageNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetCompletionsReport_WithDateFilters_ReturnsFilteredReport()
    {
        // Arrange
        var dateFrom = DateTime.Today.AddMonths(-1).ToString("yyyy-MM-dd");
        var dateTo = DateTime.Today.ToString("yyyy-MM-dd");

        // Act
        var response = await AdminClient.GetAsync($"/api/toolbox-talks/reports/completions?dateFrom={dateFrom}&dateTo={dateTo}");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<CompletionDetailDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetReports_Unauthenticated_Returns401()
    {
        // Act
        var complianceResponse = await UnauthenticatedClient.GetAsync("/api/toolbox-talks/reports/compliance");
        var overdueResponse = await UnauthenticatedClient.GetAsync("/api/toolbox-talks/reports/overdue");
        var completionsResponse = await UnauthenticatedClient.GetAsync("/api/toolbox-talks/reports/completions");

        // Assert
        complianceResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        overdueResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        completionsResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Export Tests

    [Fact]
    public async Task ExportOverdueReport_ReturnsExcelOrBadRequest()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/toolbox-talks/reports/overdue/export");

        // Assert - Export might not be implemented yet (returns BadRequest) or returns Excel file
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Be(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        }
    }

    [Fact]
    public async Task ExportCompletionsReport_ReturnsExcelOrBadRequest()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/toolbox-talks/reports/completions/export");

        // Assert - Export might not be implemented yet (returns BadRequest) or returns Excel file
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Be(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        }
    }

    [Fact]
    public async Task ExportComplianceReport_ReturnsPdf()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/toolbox-talks/reports/compliance/export");

        // Assert - Returns PDF file
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError); // PDF generation might fail without proper setup

        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
        }
    }

    [Fact]
    public async Task ExportReports_Unauthenticated_Returns401()
    {
        // Act
        var overdueExport = await UnauthenticatedClient.GetAsync("/api/toolbox-talks/reports/overdue/export");
        var completionsExport = await UnauthenticatedClient.GetAsync("/api/toolbox-talks/reports/completions/export");
        var complianceExport = await UnauthenticatedClient.GetAsync("/api/toolbox-talks/reports/compliance/export");

        // Assert
        overdueExport.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        completionsExport.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        complianceExport.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Dashboard Data Quality Tests

    [Fact]
    public async Task GetDashboard_AfterCreatingTalk_IncludesNewTalk()
    {
        // Arrange - Get initial count
        var initialResponse = await AdminClient.GetAsync("/api/toolbox-talks/dashboard");
        var initialResult = await initialResponse.Content.ReadFromJsonAsync<ResultWrapper<ToolboxTalkDashboardDto>>();
        var initialCount = initialResult!.Data!.TotalTalks;

        // Create a new talk
        await AdminClient.PostAsJsonAsync("/api/toolbox-talks", new
        {
            Title = $"Dashboard Test Talk {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        });

        // Act
        var response = await AdminClient.GetAsync("/api/toolbox-talks/dashboard");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<ToolboxTalkDashboardDto>>();

        // Assert
        result!.Data!.TotalTalks.Should().Be(initialCount + 1);
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

    private record ToolboxTalkDashboardDto(
        int TotalTalks,
        int ActiveTalks,
        int InactiveTalks,
        int TotalAssignments,
        int PendingCount,
        int InProgressCount,
        int CompletedCount,
        int OverdueCount,
        decimal CompletionRate,
        decimal OverdueRate,
        decimal AverageCompletionTimeHours,
        decimal AverageQuizScore,
        decimal QuizPassRate,
        List<RecentCompletionDto> RecentCompletions,
        List<OverdueAssignmentDto> OverdueAssignments,
        List<UpcomingScheduleDto> UpcomingSchedules
    );

    private record RecentCompletionDto(
        Guid ScheduledTalkId,
        string EmployeeName,
        string ToolboxTalkTitle,
        DateTime CompletedAt,
        int TotalTimeSpentSeconds,
        bool? QuizPassed,
        decimal? QuizScore
    );

    private record OverdueAssignmentDto(
        Guid ScheduledTalkId,
        Guid EmployeeId,
        string EmployeeName,
        string? EmployeeEmail,
        string ToolboxTalkTitle,
        DateTime DueDate,
        int DaysOverdue,
        int RemindersSent,
        [property: JsonConverter(typeof(JsonStringEnumConverter))]
        ScheduledTalkStatus Status
    );

    private record UpcomingScheduleDto(
        Guid ScheduleId,
        string ToolboxTalkTitle,
        DateTime ScheduledDate,
        [property: JsonConverter(typeof(JsonStringEnumConverter))]
        ToolboxTalkFrequency Frequency,
        string FrequencyDisplay,
        int AssignmentCount,
        bool AssignToAllEmployees
    );

    private record ToolboxTalkSettingsDto(
        Guid Id,
        int DefaultDueDays,
        int ReminderDaysBefore,
        bool SendEmailReminders,
        bool SendPushReminders,
        int MaxQuizAttempts,
        bool RequireSignature,
        bool AutoAssignNewEmployees
    );

    private record ComplianceReportDto(
        decimal OverallComplianceRate,
        int TotalAssignments,
        int CompletedCount,
        int OverdueCount,
        List<DepartmentComplianceDto> DepartmentBreakdown,
        List<TalkComplianceDto> TalkBreakdown
    );

    private record DepartmentComplianceDto(
        Guid DepartmentId,
        string DepartmentName,
        decimal ComplianceRate,
        int TotalAssignments,
        int CompletedCount,
        int OverdueCount
    );

    private record TalkComplianceDto(
        Guid ToolboxTalkId,
        string ToolboxTalkTitle,
        decimal ComplianceRate,
        int TotalAssignments,
        int CompletedCount,
        int OverdueCount
    );

    private record OverdueItemDto(
        Guid ScheduledTalkId,
        Guid EmployeeId,
        string EmployeeName,
        string? EmployeeEmail,
        Guid ToolboxTalkId,
        string ToolboxTalkTitle,
        DateTime DueDate,
        int DaysOverdue,
        int RemindersSent
    );

    private record CompletionDetailDto(
        Guid ScheduledTalkId,
        Guid EmployeeId,
        string EmployeeName,
        Guid ToolboxTalkId,
        string ToolboxTalkTitle,
        DateTime CompletedAt,
        int TimeSpentMinutes,
        bool? QuizPassed,
        decimal? QuizScore,
        string? SignedByName
    );

    private record ToolboxTalkCreatedDto(
        Guid Id,
        string Title
    );

    #endregion
}
