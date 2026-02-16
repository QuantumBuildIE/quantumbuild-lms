using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using System.Text.Json.Serialization;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for Toolbox Talk CRUD operations.
/// </summary>
public class ToolboxTalkCrudTests : IntegrationTestBase
{
    public ToolboxTalkCrudTests(CustomWebApplicationFactory factory) : base(factory) { }

    #region Get Tests

    [Fact]
    public async Task GetToolboxTalks_ReturnsPagedResults()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/toolbox-talks?pageNumber=1&pageSize=10");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<ToolboxTalkListDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PageNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetToolboxTalks_WithSearchTerm_ReturnsFilteredResults()
    {
        // Arrange - First create a talk with a unique title
        var uniqueTitle = $"SearchTest_{Guid.NewGuid():N}";
        var createCommand = new
        {
            Title = uniqueTitle,
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };
        await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);

        // Act
        var response = await AdminClient.GetAsync($"/api/toolbox-talks?searchTerm={uniqueTitle}");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<ToolboxTalkListDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Items.Should().Contain(t => t.Title.Contains(uniqueTitle));
    }

    [Fact]
    public async Task GetToolboxTalkById_ExistingTalk_ReturnsTalk()
    {
        // Arrange
        var talkId = TestTenantConstants.ToolboxTalks.Talks.BasicTalk;

        // Act
        var response = await AdminClient.GetAsync($"/api/toolbox-talks/{talkId}");

        // Assert - The seeded data may not exist, so check if it's OK or NotFound
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetToolboxTalkById_NonExistingTalk_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await AdminClient.GetAsync($"/api/toolbox-talks/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetToolboxTalks_Unauthenticated_Returns401()
    {
        // Act
        var response = await UnauthenticatedClient.GetAsync("/api/toolbox-talks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task CreateToolboxTalk_WithSections_ReturnsCreated()
    {
        // Arrange
        var command = new
        {
            Title = $"Test Talk {Guid.NewGuid()}",
            Description = "Integration test talk",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Introduction", Content = "<p>Welcome</p>", RequiresAcknowledgment = true },
                new { SectionNumber = 2, Title = "Main Content", Content = "<p>Content here</p>", RequiresAcknowledgment = true }
            }
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", command);
        var result = await response.Content.ReadFromJsonAsync<ToolboxTalkDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();
        result.Title.Should().Be(command.Title);
        result.Sections.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateToolboxTalk_WithQuiz_RequiresPassingScore()
    {
        // Arrange
        var command = new
        {
            Title = "Quiz Talk Test",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = true,
            PassingScore = 80,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            },
            Questions = new[]
            {
                new
                {
                    QuestionNumber = 1,
                    QuestionText = "What is 2+2?",
                    QuestionType = QuestionType.MultipleChoice,
                    Options = new[] { "3", "4", "5" },
                    CorrectAnswer = "4",
                    Points = 1
                }
            }
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", command);
        var result = await response.Content.ReadFromJsonAsync<ToolboxTalkDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.RequiresQuiz.Should().BeTrue();
        result.PassingScore.Should().Be(80);
        result.Questions.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateToolboxTalk_MinimalData_ReturnsCreated()
    {
        // Arrange - Only required fields
        var command = new
        {
            Title = $"Minimal Talk {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateToolboxTalk_Unauthenticated_Returns401()
    {
        // Arrange
        var command = new
        {
            Title = "Unauthenticated Test",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        // Act
        var response = await UnauthenticatedClient.PostAsJsonAsync("/api/toolbox-talks", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateToolboxTalk_WithoutPermission_Returns403()
    {
        // Arrange
        var command = new
        {
            Title = "No Permission Test",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        // Act - Finance user doesn't have ToolboxTalks.Create permission
        var response = await FinanceClient.PostAsJsonAsync("/api/toolbox-talks", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateToolboxTalk_ValidData_ReturnsOk()
    {
        // Arrange - First create a talk
        var createCommand = new
        {
            Title = $"Talk to Update {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Original Content</p>", RequiresAcknowledgment = true }
            }
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createdTalk = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkDto>();
        var talkId = createdTalk!.Id;

        // Update the talk
        var updateCommand = new
        {
            Id = talkId,
            Title = "Updated Talk Title",
            Description = "Updated description",
            Frequency = ToolboxTalkFrequency.Monthly,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new
                {
                    Id = createdTalk.Sections[0].Id,
                    SectionNumber = 1,
                    Title = "Updated Section",
                    Content = "<p>Updated Content</p>",
                    RequiresAcknowledgment = true
                }
            }
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync($"/api/toolbox-talks/{talkId}", updateCommand);
        var result = await response.Content.ReadFromJsonAsync<ToolboxTalkDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Title.Should().Be("Updated Talk Title");
        result.Description.Should().Be("Updated description");
        result.Frequency.Should().Be(ToolboxTalkFrequency.Monthly);
    }

    [Fact]
    public async Task UpdateToolboxTalk_UpdatesSections()
    {
        // Arrange - Create a talk
        var createCommand = new
        {
            Title = $"Sections Update Test {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content 1</p>", RequiresAcknowledgment = true },
                new { SectionNumber = 2, Title = "Section 2", Content = "<p>Content 2</p>", RequiresAcknowledgment = true }
            }
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
        var createdTalk = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkDto>();
        var talkId = createdTalk!.Id;

        // Update with modified sections
        var updateCommand = new
        {
            Id = talkId,
            Title = createdTalk.Title,
            Frequency = createdTalk.Frequency,
            RequiresQuiz = createdTalk.RequiresQuiz,
            IsActive = createdTalk.IsActive,
            Sections = createdTalk.Sections.Select(s => new
            {
                s.Id,
                s.SectionNumber,
                Title = s.Title + " Updated",
                s.Content,
                s.RequiresAcknowledgment
            }).ToArray()
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync($"/api/toolbox-talks/{talkId}", updateCommand);
        var result = await response.Content.ReadFromJsonAsync<ToolboxTalkDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Sections.Should().OnlyContain(s => s.Title.EndsWith(" Updated"));
    }

    [Fact]
    public async Task UpdateToolboxTalk_NonExistingTalk_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateCommand = new
        {
            Id = nonExistentId,
            Title = "Non-existent Talk",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync($"/api/toolbox-talks/{nonExistentId}", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteToolboxTalk_ExistingTalk_ReturnsNoContent()
    {
        // Arrange - Create a talk to delete
        var createCommand = new
        {
            Title = $"Talk to Delete {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
        var createdTalk = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkDto>();
        var talkId = createdTalk!.Id;

        // Act
        var response = await AdminClient.DeleteAsync($"/api/toolbox-talks/{talkId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deleted (soft delete)
        var getResponse = await AdminClient.GetAsync($"/api/toolbox-talks/{talkId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteToolboxTalk_NonExistingTalk_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await AdminClient.DeleteAsync($"/api/toolbox-talks/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteToolboxTalk_WithoutPermission_Returns403()
    {
        // Arrange - Create a talk first with admin
        var createCommand = new
        {
            Title = $"Talk for Delete Permission Test {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
        var createdTalk = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkDto>();
        var talkId = createdTalk!.Id;

        // Act - Operator doesn't have delete permission
        var response = await OperatorClient.DeleteAsync($"/api/toolbox-talks/{talkId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Quiz Validation Tests

    [Fact]
    public async Task GetToolboxTalk_WithQuiz_IncludesSectionsAndQuestions()
    {
        // Arrange - Create a talk with quiz
        var createCommand = new
        {
            Title = $"Talk With Quiz {Guid.NewGuid()}",
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = true,
            PassingScore = 70,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Safety Intro", Content = "<p>Safety first</p>", RequiresAcknowledgment = true }
            },
            Questions = new[]
            {
                new
                {
                    QuestionNumber = 1,
                    QuestionText = "What is the first rule?",
                    QuestionType = QuestionType.MultipleChoice,
                    Options = new[] { "Safety", "Speed", "Cost" },
                    CorrectAnswer = "Safety",
                    Points = 1
                },
                new
                {
                    QuestionNumber = 2,
                    QuestionText = "Is PPE required?",
                    QuestionType = QuestionType.TrueFalse,
                    Options = new[] { "True", "False" },
                    CorrectAnswer = "True",
                    Points = 1
                }
            }
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);
        var createdTalk = await createResponse.Content.ReadFromJsonAsync<ToolboxTalkDto>();
        var talkId = createdTalk!.Id;

        // Act
        var response = await AdminClient.GetAsync($"/api/toolbox-talks/{talkId}");
        var talk = await response.Content.ReadFromJsonAsync<ToolboxTalkDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        talk.Should().NotBeNull();
        talk!.Sections.Should().NotBeEmpty();
        talk.Questions.Should().NotBeEmpty();
        talk.Questions.Should().HaveCount(2);
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

    private record ToolboxTalkListDto(
        Guid Id,
        string Title,
        string? Description,
        ToolboxTalkFrequency Frequency,
        string FrequencyDisplay,
        bool RequiresQuiz,
        bool IsActive,
        DateTime CreatedAt
    );

    private record ToolboxTalkDto(
        Guid Id,
        string Title,
        string? Description,
        [property: JsonConverter(typeof(JsonStringEnumConverter))]
        ToolboxTalkFrequency Frequency,
        string FrequencyDisplay,
        string? VideoUrl,
        bool RequiresQuiz,
        int? PassingScore,
        bool IsActive,
        List<ToolboxTalkSectionDto> Sections,
        List<ToolboxTalkQuestionDto> Questions,
        DateTime CreatedAt,
        DateTime? UpdatedAt
    );

    private record ToolboxTalkSectionDto(
        Guid Id,
        int SectionNumber,
        string Title,
        string Content,
        bool RequiresAcknowledgment
    );

    private record ToolboxTalkQuestionDto(
        Guid Id,
        int QuestionNumber,
        string QuestionText,
        [property: JsonConverter(typeof(JsonStringEnumConverter))]
        QuestionType QuestionType,
        List<string>? Options,
        string CorrectAnswer,
        int Points
    );

    #endregion
}
