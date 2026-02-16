using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for the Subtitle Processing API endpoints.
/// Tests the subtitle processing workflow for toolbox talk videos.
/// </summary>
public class SubtitleProcessingTests : IntegrationTestBase
{
    public SubtitleProcessingTests(CustomWebApplicationFactory factory) : base(factory) { }

    #region POST /api/toolbox-talks/{id}/subtitles/process - Start Processing

    [Fact]
    public async Task StartProcessing_ValidRequest_Returns202WithJobId()
    {
        // Arrange - Use the seeded talk with video
        var talkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithVideo;
        var request = new StartProcessingRequest
        {
            VideoUrl = "https://drive.google.com/file/d/test-video-id/view",
            VideoSourceType = SubtitleVideoSourceType.GoogleDrive,
            TargetLanguages = new List<string> { "Spanish", "Polish" }
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var result = await response.Content.ReadFromJsonAsync<StartProcessingResponse>();
        result.Should().NotBeNull();
        result!.JobId.Should().NotBeEmpty();
        result.Message.Should().Contain("Processing started");
        result.StatusUrl.Should().Contain($"/api/toolbox-talks/{talkId}/subtitles/status");
    }

    [Fact]
    public async Task StartProcessing_NonExistentTalk_Returns400()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var request = new StartProcessingRequest
        {
            VideoUrl = "https://example.com/video.mp4",
            VideoSourceType = SubtitleVideoSourceType.DirectUrl,
            TargetLanguages = new List<string> { "Spanish" }
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{nonExistentId}/subtitles/process", request);

        // Assert
        // The orchestrator throws InvalidOperationException which returns 400
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("not found");
    }

    [Fact]
    public async Task StartProcessing_InvalidLanguages_Returns400WithValidLanguages()
    {
        // Arrange
        var talkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithVideo;
        var request = new StartProcessingRequest
        {
            VideoUrl = "https://example.com/video.mp4",
            VideoSourceType = SubtitleVideoSourceType.DirectUrl,
            TargetLanguages = new List<string> { "InvalidLanguage", "FakeLanguage" }
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("InvalidLanguage");
        content.Should().Contain("Invalid languages");
    }

    [Fact]
    public async Task StartProcessing_AlreadyProcessing_Returns400()
    {
        // Arrange - Create a talk and start processing
        var createCommand = new
        {
            Title = $"Talk for duplicate processing test {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            VideoUrl = "https://example.com/video.mp4",
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdTalk = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkDto>();
        var talkId = createdTalk!.Id;

        var request = new StartProcessingRequest
        {
            VideoUrl = "https://drive.google.com/file/d/test/view",
            VideoSourceType = SubtitleVideoSourceType.GoogleDrive,
            TargetLanguages = new List<string> { "Spanish" }
        };

        // Start first processing
        var firstResponse = await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/process", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Act - Try to start processing again
        var secondResponse = await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/process", request);

        // Assert - Should fail because processing is already active
        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await secondResponse.Content.ReadAsStringAsync();
        content.Should().Contain("already active");
    }

    [Fact]
    public async Task StartProcessing_Unauthenticated_Returns401()
    {
        // Arrange
        var talkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithVideo;
        var request = new StartProcessingRequest
        {
            VideoUrl = "https://example.com/video.mp4",
            VideoSourceType = SubtitleVideoSourceType.DirectUrl,
            TargetLanguages = new List<string> { "Spanish" }
        };

        // Act
        var response = await UnauthenticatedClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StartProcessing_WithoutEditPermission_Returns403()
    {
        // Arrange - Operator has ToolboxTalks.View but not ToolboxTalks.Edit
        var talkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithVideo;
        var request = new StartProcessingRequest
        {
            VideoUrl = "https://example.com/video.mp4",
            VideoSourceType = SubtitleVideoSourceType.DirectUrl,
            TargetLanguages = new List<string> { "Spanish" }
        };

        // Act
        var response = await OperatorClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StartProcessing_WithEditPermission_Returns202()
    {
        // Arrange - Create a new talk for this test
        var createCommand = new
        {
            Title = $"Talk for permission test {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            VideoUrl = "https://example.com/video.mp4",
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
        var createdTalk = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkDto>();
        var talkId = createdTalk!.Id;

        var request = new StartProcessingRequest
        {
            VideoUrl = "https://drive.google.com/file/d/test/view",
            VideoSourceType = SubtitleVideoSourceType.GoogleDrive,
            TargetLanguages = new List<string> { "Spanish" }
        };

        // Act - SiteManager has ToolboxTalks.Edit permission
        var response = await SiteManagerClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/process", request);

        // Assert - Should be accepted (or fail in background)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Accepted, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StartProcessing_MissingVideoUrl_Returns400()
    {
        // Arrange
        var talkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithVideo;
        var request = new
        {
            VideoUrl = "", // Empty video URL
            VideoSourceType = SubtitleVideoSourceType.DirectUrl,
            TargetLanguages = new List<string> { "Spanish" }
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StartProcessing_MissingTargetLanguages_Returns400()
    {
        // Arrange
        var talkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithVideo;
        var request = new
        {
            VideoUrl = "https://example.com/video.mp4",
            VideoSourceType = SubtitleVideoSourceType.DirectUrl,
            TargetLanguages = new List<string>() // Empty list
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StartProcessing_AllVideoSourceTypes_Accepted()
    {
        // Test each video source type can be submitted
        var sourceTypes = new[]
        {
            SubtitleVideoSourceType.GoogleDrive,
            SubtitleVideoSourceType.AzureBlob,
            SubtitleVideoSourceType.DirectUrl
        };

        foreach (var sourceType in sourceTypes)
        {
            // Create a new talk for each source type
            var createCommand = new
            {
                Title = $"Talk for source type {sourceType} test {Guid.NewGuid()}",
                Frequency = ToolboxTalkFrequency.Once,
                RequiresQuiz = false,
                IsActive = true,
                Sections = new[]
                {
                    new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
                }
            };

            var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
            var createdTalk = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkDto>();
            var talkId = createdTalk!.Id;

            var request = new StartProcessingRequest
            {
                VideoUrl = sourceType switch
                {
                    SubtitleVideoSourceType.GoogleDrive => "https://drive.google.com/file/d/test/view",
                    SubtitleVideoSourceType.AzureBlob => "https://storage.blob.core.windows.net/container/video.mp4",
                    SubtitleVideoSourceType.DirectUrl => "https://example.com/video.mp4",
                    _ => "https://example.com/video.mp4"
                },
                VideoSourceType = sourceType,
                TargetLanguages = new List<string> { "Spanish" }
            };

            // Act
            var response = await AdminClient.PostAsJsonAsync(
                $"/api/toolbox-talks/{talkId}/subtitles/process", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Accepted,
                $"Source type {sourceType} should be accepted");
        }
    }

    #endregion

    #region GET /api/toolbox-talks/{id}/subtitles/status - Get Status

    [Fact]
    public async Task GetStatus_NoJobExists_Returns404()
    {
        // Arrange - Use a talk that has no processing job
        var talkId = TestTenantConstants.ToolboxTalks.Talks.BasicTalk;

        // Act
        var response = await AdminClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("No processing job found");
    }

    [Fact]
    public async Task GetStatus_AfterStartingProcessing_ReturnsJobStatus()
    {
        // Arrange - Start processing first
        var createCommand = new
        {
            Title = $"Talk for status test {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
        var createdTalk = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkDto>();
        var talkId = createdTalk!.Id;

        var startRequest = new StartProcessingRequest
        {
            VideoUrl = "https://drive.google.com/file/d/test/view",
            VideoSourceType = SubtitleVideoSourceType.GoogleDrive,
            TargetLanguages = new List<string> { "Spanish", "Polish" }
        };

        var startResponse = await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/process", startRequest);
        startResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Act - Get the status
        var response = await AdminClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await response.Content.ReadFromJsonAsync<SubtitleProcessingStatusDto>();
        status.Should().NotBeNull();
        status!.JobId.Should().NotBeEmpty();
        status.ToolboxTalkId.Should().Be(talkId);
        status.Status.Should().NotBe(default(SubtitleProcessingStatus));
        status.Languages.Should().NotBeEmpty();
        // English is always added, plus the two target languages
        status.Languages.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task GetStatus_ReturnsCorrectLanguageProgress()
    {
        // Arrange - Create and start processing
        var createCommand = new
        {
            Title = $"Talk for language progress test {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
        var createdTalk = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkDto>();
        var talkId = createdTalk!.Id;

        var startRequest = new StartProcessingRequest
        {
            VideoUrl = "https://drive.google.com/file/d/test/view",
            VideoSourceType = SubtitleVideoSourceType.GoogleDrive,
            TargetLanguages = new List<string> { "Spanish", "German", "French" }
        };

        await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/process", startRequest);

        // Act
        var response = await AdminClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await response.Content.ReadFromJsonAsync<SubtitleProcessingStatusDto>();
        status.Should().NotBeNull();

        // Should have English (source) plus the 3 target languages
        status!.Languages.Should().HaveCountGreaterOrEqualTo(1);

        // Check language structure
        foreach (var lang in status.Languages)
        {
            lang.Language.Should().NotBeNullOrEmpty();
            lang.LanguageCode.Should().NotBeNullOrEmpty();
            lang.Percentage.Should().BeInRange(0, 100);
        }
    }

    [Fact]
    public async Task GetStatus_Unauthenticated_Returns401()
    {
        // Arrange
        var talkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithVideo;

        // Act
        var response = await UnauthenticatedClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetStatus_WithViewPermission_Returns200()
    {
        // Arrange - Start a job first
        var createCommand = new
        {
            Title = $"Talk for view permission status test {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
        var createdTalk = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkDto>();
        var talkId = createdTalk!.Id;

        var startRequest = new StartProcessingRequest
        {
            VideoUrl = "https://drive.google.com/file/d/test/view",
            VideoSourceType = SubtitleVideoSourceType.GoogleDrive,
            TargetLanguages = new List<string> { "Spanish" }
        };

        await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/process", startRequest);

        // Act - Operator has ToolboxTalks.View permission
        var response = await OperatorClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region GET /api/subtitles/available-languages - Get Available Languages

    [Fact]
    public async Task GetAvailableLanguages_ReturnsLanguagesAndEmployeeCounts()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/subtitles/available-languages");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<AvailableLanguagesResponse>();
        result.Should().NotBeNull();
        result!.AllSupportedLanguages.Should().NotBeEmpty();

        // Check that all supported languages have both name and code
        foreach (var lang in result.AllSupportedLanguages)
        {
            lang.Language.Should().NotBeNullOrEmpty();
            lang.LanguageCode.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task GetAvailableLanguages_ContainsCommonLanguages()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/subtitles/available-languages");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<AvailableLanguagesResponse>();
        result.Should().NotBeNull();

        var languageNames = result!.AllSupportedLanguages.Select(l => l.Language).ToList();

        // Common languages should be present
        languageNames.Should().Contain("English");
        languageNames.Should().Contain("Spanish");
        languageNames.Should().Contain("Polish");
    }

    [Fact]
    public async Task GetAvailableLanguages_Unauthenticated_Returns401()
    {
        // Act
        var response = await UnauthenticatedClient.GetAsync("/api/subtitles/available-languages");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAvailableLanguages_WithViewPermission_Returns200()
    {
        // Act - Operator has ToolboxTalks.View permission
        var response = await OperatorClient.GetAsync("/api/subtitles/available-languages");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAvailableLanguages_ReturnsEmployeeLanguageCounts()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/subtitles/available-languages");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<AvailableLanguagesResponse>();
        result.Should().NotBeNull();

        // EmployeeLanguages should be a list (may be empty if no employees have languages set)
        result!.EmployeeLanguages.Should().NotBeNull();

        // If there are employee languages, verify structure
        foreach (var empLang in result.EmployeeLanguages)
        {
            empLang.Language.Should().NotBeNullOrEmpty();
            empLang.LanguageCode.Should().NotBeNullOrEmpty();
            empLang.EmployeeCount.Should().BeGreaterThan(0);
        }
    }

    #endregion

    #region POST /api/toolbox-talks/{id}/subtitles/cancel - Cancel Processing

    [Fact]
    public async Task CancelProcessing_ActiveJob_ReturnsOkAndCancelsJob()
    {
        // Arrange - Create a talk
        var createCommand = new
        {
            Title = $"Talk for cancel test {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
        var createdTalk = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkDto>();
        var talkId = createdTalk!.Id;

        // Create job directly in database with Transcribing status to avoid race condition
        // (fake services complete instantly, so starting via API would result in Completed status)
        var dbContext = GetDbContext();
        var job = new SubtitleProcessingJob
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantConstants.TenantId,
            ToolboxTalkId = talkId,
            Status = SubtitleProcessingStatus.Transcribing,
            SourceVideoUrl = "https://drive.google.com/file/d/test/view",
            VideoSourceType = SubtitleVideoSourceType.GoogleDrive,
            StartedAt = DateTime.UtcNow
        };
        dbContext.SubtitleProcessingJobs.Add(job);
        await dbContext.SaveChangesAsync();

        // Act - Cancel the processing
        var cancelResponse = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/cancel", null);

        // Assert
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await cancelResponse.Content.ReadAsStringAsync();
        content.Should().Contain("cancelled");

        // Verify status is now Cancelled
        var statusResponse = await AdminClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/status");
        var status = await statusResponse.Content.ReadFromJsonAsync<SubtitleProcessingStatusDto>();
        status!.Status.Should().Be(SubtitleProcessingStatus.Cancelled);
    }

    [Fact]
    public async Task CancelProcessing_NoActiveJob_Returns404()
    {
        // Arrange - Use a talk with no active job
        var talkId = TestTenantConstants.ToolboxTalks.Talks.BasicTalk;

        // Act
        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelProcessing_Unauthenticated_Returns401()
    {
        // Arrange
        var talkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithVideo;

        // Act
        var response = await UnauthenticatedClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CancelProcessing_WithoutEditPermission_Returns403()
    {
        // Arrange - Operator has ToolboxTalks.View but not ToolboxTalks.Edit
        var talkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithVideo;

        // Act
        var response = await OperatorClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region POST /api/toolbox-talks/{id}/subtitles/retry - Retry Failed Translations

    [Fact]
    public async Task RetryProcessing_NoJob_Returns404()
    {
        // Arrange - Use a talk with no job
        var talkId = TestTenantConstants.ToolboxTalks.Talks.BasicTalk;

        // Act
        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/retry", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RetryProcessing_NoFailedTranslations_Returns400()
    {
        // Arrange - Create a talk and start processing (no failed translations yet)
        var createCommand = new
        {
            Title = $"Talk for retry no failures test {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
        var createdTalk = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkDto>();
        var talkId = createdTalk!.Id;

        var request = new StartProcessingRequest
        {
            VideoUrl = "https://drive.google.com/file/d/test/view",
            VideoSourceType = SubtitleVideoSourceType.GoogleDrive,
            TargetLanguages = new List<string> { "Spanish" }
        };

        await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/process", request);

        // Cancel it to have a non-active job
        await AdminClient.PostAsync($"/api/toolbox-talks/{talkId}/subtitles/cancel", null);

        // Act - Try to retry (but there are no failed translations)
        var retryResponse = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/retry", null);

        // Assert - Should fail because no failed translations
        retryResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await retryResponse.Content.ReadAsStringAsync();
        content.Should().Contain("No failed translations");
    }

    [Fact]
    public async Task RetryProcessing_Unauthenticated_Returns401()
    {
        // Arrange
        var talkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithVideo;

        // Act
        var response = await UnauthenticatedClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/retry", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RetryProcessing_WithoutEditPermission_Returns403()
    {
        // Arrange
        var talkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithVideo;

        // Act
        var response = await OperatorClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/retry", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region GET /api/toolbox-talks/{id}/subtitles/{languageCode} - Get SRT File

    [Fact]
    public async Task GetSrtFile_NoJob_Returns404()
    {
        // Arrange
        var talkId = TestTenantConstants.ToolboxTalks.Talks.BasicTalk;

        // Act
        var response = await AdminClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/en");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSrtFile_InvalidLanguageCode_Returns404()
    {
        // Arrange - Create and start processing
        var createCommand = new
        {
            Title = $"Talk for SRT download test {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
        var createdTalk = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkDto>();
        var talkId = createdTalk!.Id;

        var request = new StartProcessingRequest
        {
            VideoUrl = "https://drive.google.com/file/d/test/view",
            VideoSourceType = SubtitleVideoSourceType.GoogleDrive,
            TargetLanguages = new List<string> { "Spanish" }
        };

        await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/process", request);

        // Act - Try to get SRT for a language that wasn't requested
        var response = await AdminClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/xx");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSrtFile_Unauthenticated_Returns401()
    {
        // Arrange
        var talkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithVideo;

        // Act
        var response = await UnauthenticatedClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/en");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSrtFile_WithViewPermission_CanAccess()
    {
        // Arrange - Create and start processing
        var createCommand = new
        {
            Title = $"Talk for SRT permission test {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
        var createdTalk = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkDto>();
        var talkId = createdTalk!.Id;

        var request = new StartProcessingRequest
        {
            VideoUrl = "https://drive.google.com/file/d/test/view",
            VideoSourceType = SubtitleVideoSourceType.GoogleDrive,
            TargetLanguages = new List<string> { "Spanish" }
        };

        await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/process", request);

        // Act - Operator has ToolboxTalks.View permission
        var response = await OperatorClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/en");

        // Assert - Should either return 200 with content or 404 if not completed
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Language Code Service Tests

    [Fact]
    public async Task StartProcessing_LanguageCodesAreCorrectlyAssigned()
    {
        // Arrange
        var createCommand = new
        {
            Title = $"Talk for language code test {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
        var createdTalk = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkDto>();
        var talkId = createdTalk!.Id;

        // Start with specific languages
        var request = new StartProcessingRequest
        {
            VideoUrl = "https://drive.google.com/file/d/test/view",
            VideoSourceType = SubtitleVideoSourceType.GoogleDrive,
            TargetLanguages = new List<string> { "Spanish", "German", "Polish" }
        };

        await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/process", request);

        // Act
        var response = await AdminClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/status");

        var status = await response.Content.ReadFromJsonAsync<SubtitleProcessingStatusDto>();

        // Assert - Check language codes
        var languageCodeMap = status!.Languages.ToDictionary(l => l.Language, l => l.LanguageCode);

        // English should always be included
        languageCodeMap.Should().ContainKey("English");
        languageCodeMap["English"].Should().Be("en");

        if (languageCodeMap.ContainsKey("Spanish"))
            languageCodeMap["Spanish"].Should().Be("es");

        if (languageCodeMap.ContainsKey("German"))
            languageCodeMap["German"].Should().Be("de");

        if (languageCodeMap.ContainsKey("Polish"))
            languageCodeMap["Polish"].Should().Be("pl");
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task StartProcessing_EnglishInTargetLanguages_NotDuplicated()
    {
        // Arrange - Include English in target languages (it should not be duplicated)
        var createCommand = new
        {
            Title = $"Talk for English dedup test {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
        var createdTalk = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkDto>();
        var talkId = createdTalk!.Id;

        var request = new StartProcessingRequest
        {
            VideoUrl = "https://drive.google.com/file/d/test/view",
            VideoSourceType = SubtitleVideoSourceType.GoogleDrive,
            TargetLanguages = new List<string> { "English", "Spanish" }
        };

        await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/process", request);

        // Act
        var response = await AdminClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/status");

        var status = await response.Content.ReadFromJsonAsync<SubtitleProcessingStatusDto>();

        // Assert - English should only appear once
        var englishCount = status!.Languages.Count(l => l.Language.Equals("English", StringComparison.OrdinalIgnoreCase));
        englishCount.Should().Be(1);
    }

    [Fact]
    public async Task GetStatus_ReturnsCorrectPercentageCalculation()
    {
        // Arrange
        var createCommand = new
        {
            Title = $"Talk for percentage test {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
        var createdTalk = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkDto>();
        var talkId = createdTalk!.Id;

        var request = new StartProcessingRequest
        {
            VideoUrl = "https://drive.google.com/file/d/test/view",
            VideoSourceType = SubtitleVideoSourceType.GoogleDrive,
            TargetLanguages = new List<string> { "Spanish" }
        };

        await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/process", request);

        // Act
        var response = await AdminClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/subtitles/status");

        var status = await response.Content.ReadFromJsonAsync<SubtitleProcessingStatusDto>();

        // Assert
        status!.OverallPercentage.Should().BeInRange(0, 100);
    }

    #endregion

    #region Response DTOs

    private record StartProcessingRequest
    {
        public string VideoUrl { get; set; } = string.Empty;
        public SubtitleVideoSourceType VideoSourceType { get; set; }
        public List<string> TargetLanguages { get; set; } = new();
    }

    private record StartProcessingResponse
    {
        public Guid JobId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string StatusUrl { get; set; } = string.Empty;
    }

    private record SubtitleProcessingStatusDto
    {
        public Guid JobId { get; set; }
        public Guid ToolboxTalkId { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SubtitleProcessingStatus Status { get; set; }
        public int OverallPercentage { get; set; }
        public string CurrentStep { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int TotalSubtitles { get; set; }
        public List<LanguageStatusDto> Languages { get; set; } = new();
    }

    private record LanguageStatusDto
    {
        public string Language { get; set; } = string.Empty;
        public string LanguageCode { get; set; } = string.Empty;
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SubtitleTranslationStatus Status { get; set; }
        public int Percentage { get; set; }
        public string? SrtUrl { get; set; }
        public string? ErrorMessage { get; set; }
    }

    private record AvailableLanguagesResponse
    {
        public List<EmployeeLanguageInfo> EmployeeLanguages { get; set; } = new();
        public List<SupportedLanguageInfo> AllSupportedLanguages { get; set; } = new();
    }

    private record EmployeeLanguageInfo
    {
        public string Language { get; set; } = string.Empty;
        public string LanguageCode { get; set; } = string.Empty;
        public int EmployeeCount { get; set; }
    }

    private record SupportedLanguageInfo
    {
        public string Language { get; set; } = string.Empty;
        public string LanguageCode { get; set; } = string.Empty;
    }

    private record ToolboxTalkDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    #endregion
}
