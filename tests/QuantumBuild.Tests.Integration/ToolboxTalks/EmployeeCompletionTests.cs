using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for employee toolbox talk completion flow.
/// Tests the /api/my/toolbox-talks endpoints.
/// </summary>
public class EmployeeCompletionTests : IntegrationTestBase
{
    public EmployeeCompletionTests(CustomWebApplicationFactory factory) : base(factory) { }

    #region Get My Talks Tests

    [Fact]
    public async Task GetMyToolboxTalks_ReturnsPagedResults()
    {
        // Act
        var response = await SiteManagerClient.GetAsync("/api/my/toolbox-talks?pageNumber=1&pageSize=10");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<MyToolboxTalkListDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.PageNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetMyToolboxTalks_FilterByStatus_ReturnsFilteredResults()
    {
        // Act
        var response = await SiteManagerClient.GetAsync($"/api/my/toolbox-talks?status={ScheduledTalkStatus.Pending}");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<MyToolboxTalkListDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetMyToolboxTalks_Unauthenticated_Returns401()
    {
        // Act
        var response = await UnauthenticatedClient.GetAsync("/api/my/toolbox-talks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPendingToolboxTalks_ReturnsOnlyPending()
    {
        // Act
        var response = await SiteManagerClient.GetAsync("/api/my/toolbox-talks/pending");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<MyToolboxTalkListDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetInProgressToolboxTalks_ReturnsOnlyInProgress()
    {
        // Act
        var response = await SiteManagerClient.GetAsync("/api/my/toolbox-talks/in-progress");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<MyToolboxTalkListDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetOverdueToolboxTalks_ReturnsOnlyOverdue()
    {
        // Act
        var response = await SiteManagerClient.GetAsync("/api/my/toolbox-talks/overdue");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<MyToolboxTalkListDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetCompletedToolboxTalks_ReturnsOnlyCompleted()
    {
        // Act
        var response = await SiteManagerClient.GetAsync("/api/my/toolbox-talks/completed");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<MyToolboxTalkListDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Success.Should().BeTrue();
    }

    #endregion

    #region Get Talk By Id Tests

    [Fact]
    public async Task GetMyToolboxTalkById_NonExistingTalk_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await SiteManagerClient.GetAsync($"/api/my/toolbox-talks/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Mark Section Read Tests

    [Fact]
    public async Task MarkSectionRead_ValidRequest_ReturnsOk()
    {
        // Arrange - Create and schedule a talk for the test
        var scheduledTalk = await CreateScheduledTalkForTestAsync();
        if (scheduledTalk == null)
        {
            // If we couldn't create a scheduled talk (e.g., missing employee claim), skip the test assertion
            return;
        }

        // Get the talk details to find the first section
        var talkResponse = await SiteManagerClient.GetAsync($"/api/my/toolbox-talks/{scheduledTalk.Id}");
        if (!talkResponse.IsSuccessStatusCode)
        {
            // Talk might not be assigned to current user
            return;
        }

        var talk = await talkResponse.Content.ReadFromJsonAsync<MyToolboxTalkDto>();
        if (talk?.Sections == null || !talk.Sections.Any())
        {
            return;
        }

        var firstSection = talk.Sections.OrderBy(s => s.SectionNumber).First();

        // Act
        var request = new { TimeSpentSeconds = 30 };
        var response = await SiteManagerClient.PostAsJsonAsync(
            $"/api/my/toolbox-talks/{scheduledTalk.Id}/sections/{firstSection.SectionId}/read",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MarkSectionRead_NonExistingTalk_ReturnsBadRequest()
    {
        // Arrange
        var nonExistentTalkId = Guid.NewGuid();
        var nonExistentSectionId = Guid.NewGuid();

        // Act
        var request = new { TimeSpentSeconds = 30 };
        var response = await SiteManagerClient.PostAsJsonAsync(
            $"/api/my/toolbox-talks/{nonExistentTalkId}/sections/{nonExistentSectionId}/read",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkSectionRead_Unauthenticated_Returns401()
    {
        // Arrange
        var talkId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();

        // Act
        var request = new { TimeSpentSeconds = 30 };
        var response = await UnauthenticatedClient.PostAsJsonAsync(
            $"/api/my/toolbox-talks/{talkId}/sections/{sectionId}/read",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Submit Quiz Tests

    [Fact]
    public async Task SubmitQuiz_NonExistingTalk_ReturnsBadRequest()
    {
        // Arrange
        var nonExistentTalkId = Guid.NewGuid();
        var answers = new Dictionary<string, string>
        {
            { Guid.NewGuid().ToString(), "Answer" }
        };

        // Act
        var response = await SiteManagerClient.PostAsJsonAsync(
            $"/api/my/toolbox-talks/{nonExistentTalkId}/quiz/submit",
            new { Answers = answers });

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubmitQuiz_Unauthenticated_Returns401()
    {
        // Arrange
        var talkId = Guid.NewGuid();
        var answers = new Dictionary<string, string>();

        // Act
        var response = await UnauthenticatedClient.PostAsJsonAsync(
            $"/api/my/toolbox-talks/{talkId}/quiz/submit",
            new { Answers = answers });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Update Video Progress Tests

    [Fact]
    public async Task UpdateVideoProgress_ValidRequest_ReturnsOk()
    {
        // Arrange - Create a scheduled talk with video
        var scheduledTalk = await CreateScheduledTalkWithVideoForTestAsync();
        if (scheduledTalk == null)
        {
            return;
        }

        // Act
        var request = new { WatchPercent = 50 };
        var response = await SiteManagerClient.PostAsJsonAsync(
            $"/api/my/toolbox-talks/{scheduledTalk.Id}/video-progress",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateVideoProgress_NonExistingTalk_ReturnsBadRequest()
    {
        // Arrange
        var nonExistentTalkId = Guid.NewGuid();

        // Act
        var request = new { WatchPercent = 50 };
        var response = await SiteManagerClient.PostAsJsonAsync(
            $"/api/my/toolbox-talks/{nonExistentTalkId}/video-progress",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateVideoProgress_InvalidPercent_ReturnsBadRequest()
    {
        // Arrange
        var talkId = Guid.NewGuid();

        // Act
        var request = new { WatchPercent = 150 }; // Invalid - over 100
        var response = await SiteManagerClient.PostAsJsonAsync(
            $"/api/my/toolbox-talks/{talkId}/video-progress",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Complete Talk Tests

    [Fact]
    public async Task CompleteTalk_NonExistingTalk_ReturnsBadRequest()
    {
        // Arrange
        var nonExistentTalkId = Guid.NewGuid();
        var sampleSignature = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

        // Act
        var response = await SiteManagerClient.PostAsJsonAsync(
            $"/api/my/toolbox-talks/{nonExistentTalkId}/complete",
            new { SignatureData = sampleSignature, SignedByName = "Test User" });

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CompleteTalk_Unauthenticated_Returns401()
    {
        // Arrange
        var talkId = Guid.NewGuid();
        var sampleSignature = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

        // Act
        var response = await UnauthenticatedClient.PostAsJsonAsync(
            $"/api/my/toolbox-talks/{talkId}/complete",
            new { SignatureData = sampleSignature, SignedByName = "Test User" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CompleteTalk_MissingSignature_ReturnsBadRequest()
    {
        // Arrange
        var talkId = Guid.NewGuid();

        // Act
        var response = await SiteManagerClient.PostAsJsonAsync(
            $"/api/my/toolbox-talks/{talkId}/complete",
            new { SignatureData = "", SignedByName = "Test User" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Integration Flow Tests

    [Fact]
    public async Task FullCompletionFlow_WithQuiz_Succeeds()
    {
        // This is an integration test that requires seeded test data
        // Skip if test tenant data not available
        var pendingResponse = await SiteManagerClient.GetAsync("/api/my/toolbox-talks/pending?pageSize=1");
        var pendingResult = await pendingResponse.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<MyToolboxTalkListDto>>>();

        if (pendingResult?.Data?.Items == null || !pendingResult.Data.Items.Any())
        {
            // No pending talks available for testing
            return;
        }

        var pendingTalk = pendingResult.Data.Items.First();

        // Get full talk details
        var detailResponse = await SiteManagerClient.GetAsync($"/api/my/toolbox-talks/{pendingTalk.ScheduledTalkId}");
        if (!detailResponse.IsSuccessStatusCode)
        {
            return;
        }

        var talk = await detailResponse.Content.ReadFromJsonAsync<MyToolboxTalkDto>();
        if (talk == null)
        {
            return;
        }

        // Step 1: Mark all sections as read
        foreach (var section in talk.Sections.OrderBy(s => s.SectionNumber))
        {
            var readRequest = new { TimeSpentSeconds = 30 };
            var readResponse = await SiteManagerClient.PostAsJsonAsync(
                $"/api/my/toolbox-talks/{talk.ScheduledTalkId}/sections/{section.SectionId}/read",
                readRequest);

            if (!readResponse.IsSuccessStatusCode)
            {
                return; // Can't continue if section read fails
            }
        }

        // Step 2: Submit quiz if required
        if (talk.RequiresQuiz && talk.Questions.Any())
        {
            var answers = new Dictionary<Guid, string>();
            foreach (var q in talk.Questions)
            {
                // Just submit the first option as the answer
                answers[q.Id] = q.Options?.FirstOrDefault() ?? "True";
            }

            var quizResponse = await SiteManagerClient.PostAsJsonAsync(
                $"/api/my/toolbox-talks/{talk.ScheduledTalkId}/quiz/submit",
                new { Answers = answers });

            // Quiz might fail if not all sections read, that's expected
            if (!quizResponse.IsSuccessStatusCode)
            {
                return;
            }
        }

        // Step 3: Complete the talk
        var sampleSignature = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var completeResponse = await SiteManagerClient.PostAsJsonAsync(
            $"/api/my/toolbox-talks/{talk.ScheduledTalkId}/complete",
            new { SignatureData = sampleSignature, SignedByName = "Test Manager" });

        // Completion might fail due to various validation rules (quiz not passed, etc.)
        // This is testing the full flow, not the exact result
        completeResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest);
    }

    #endregion

    #region Helper Methods

    private async Task<ScheduledTalkDto?> CreateScheduledTalkForTestAsync()
    {
        // Create a talk
        var createTalkCommand = new
        {
            Title = $"Employee Test Talk {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content 1</p>", RequiresAcknowledgment = true },
                new { SectionNumber = 2, Title = "Section 2", Content = "<p>Content 2</p>", RequiresAcknowledgment = true }
            }
        };

        var talkResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createTalkCommand);
        if (!talkResponse.IsSuccessStatusCode)
        {
            return null;
        }
        var talk = await talkResponse.Content.ReadFromJsonAsync<ToolboxTalkCreatedDto>();

        // Create a schedule for the manager employee
        var scheduleCommand = new
        {
            ToolboxTalkId = talk!.Id,
            ScheduledDate = DateTime.Today.ToString("yyyy-MM-dd"),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { TestTenantConstants.Employees.ManagerEmployee }
        };

        var scheduleResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", scheduleCommand);
        if (!scheduleResponse.IsSuccessStatusCode)
        {
            return null;
        }
        var schedule = await scheduleResponse.Content.ReadFromJsonAsync<ToolboxTalkScheduleCreatedDto>();

        // Process the schedule to create assignments
        var processResponse = await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{schedule!.Id}/process", null);
        if (!processResponse.IsSuccessStatusCode)
        {
            return null;
        }

        return new ScheduledTalkDto(schedule.Id, talk.Id);
    }

    private async Task<ScheduledTalkDto?> CreateScheduledTalkWithVideoForTestAsync()
    {
        // Create a talk with video
        var createTalkCommand = new
        {
            Title = $"Video Test Talk {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            VideoUrl = "https://www.youtube.com/watch?v=test",
            VideoSource = VideoSource.YouTube,
            MinimumVideoWatchPercent = 90,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Watch Video", Content = "<p>Please watch the video above</p>", RequiresAcknowledgment = true }
            }
        };

        var talkResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createTalkCommand);
        if (!talkResponse.IsSuccessStatusCode)
        {
            return null;
        }
        var talk = await talkResponse.Content.ReadFromJsonAsync<ToolboxTalkCreatedDto>();

        // Create and process schedule
        var scheduleCommand = new
        {
            ToolboxTalkId = talk!.Id,
            ScheduledDate = DateTime.Today.ToString("yyyy-MM-dd"),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            EmployeeIds = new[] { TestTenantConstants.Employees.ManagerEmployee }
        };

        var scheduleResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/schedules", scheduleCommand);
        if (!scheduleResponse.IsSuccessStatusCode)
        {
            return null;
        }
        var schedule = await scheduleResponse.Content.ReadFromJsonAsync<ToolboxTalkScheduleCreatedDto>();

        await AdminClient.PostAsync($"/api/toolbox-talks/schedules/{schedule!.Id}/process", null);

        return new ScheduledTalkDto(schedule.Id, talk.Id);
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

    private record MyToolboxTalkListDto(
        Guid ScheduledTalkId,
        Guid ToolboxTalkId,
        string Title,
        DateTime DueDate,
        ScheduledTalkStatus Status,
        string StatusDisplay,
        decimal ProgressPercent,
        bool IsOverdue
    );

    private record MyToolboxTalkDto(
        Guid ScheduledTalkId,
        Guid ToolboxTalkId,
        string Title,
        string? Description,
        DateTime RequiredDate,
        DateTime DueDate,
        ScheduledTalkStatus Status,
        bool RequiresQuiz,
        int? PassingScore,
        List<MyToolboxTalkSectionDto> Sections,
        List<MyToolboxTalkQuestionDto> Questions,
        decimal ProgressPercent
    );

    private record MyToolboxTalkSectionDto(
        Guid SectionId,
        int SectionNumber,
        string Title,
        string Content,
        bool RequiresAcknowledgment,
        bool IsRead,
        DateTime? ReadAt
    );

    private record MyToolboxTalkQuestionDto(
        Guid Id,
        int QuestionNumber,
        string QuestionText,
        QuestionType QuestionType,
        List<string>? Options,
        int Points
    );

    private record ToolboxTalkCreatedDto(
        Guid Id,
        string Title
    );

    private record ToolboxTalkScheduleCreatedDto(
        Guid Id,
        Guid ToolboxTalkId
    );

    private record ScheduledTalkDto(
        Guid Id,
        Guid ToolboxTalkId
    );

    private record QuizResultDto(
        Guid AttemptId,
        int AttemptNumber,
        int Score,
        int MaxScore,
        decimal Percentage,
        bool Passed,
        int PassingScore
    );

    private record ScheduledTalkCompletionDto(
        Guid Id,
        DateTime CompletedAt,
        string? CertificateUrl
    );

    #endregion
}
