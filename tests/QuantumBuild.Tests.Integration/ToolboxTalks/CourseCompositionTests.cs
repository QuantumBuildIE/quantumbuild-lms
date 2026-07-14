using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for the Draft-talk composition guard on the compose-existing
/// course endpoints (Chunk 1 of the course-in-new-wizard work). Covers:
///  - POST /api/toolbox-talks/courses rejects Draft talks
///  - PUT /api/toolbox-talks/courses/{id}/items rejects Draft talks
///  - Both succeed when all referenced talks are Published (regression coverage)
/// </summary>
[Collection("Integration")]
public class CourseCompositionTests : IntegrationTestBase
{
    public CourseCompositionTests(CustomWebApplicationFactory factory) : base(factory) { }

    private static string UniqueTitle(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..40];

    /// <summary>Creates a talk with one section via the CRUD endpoint. New talks default to Draft.</summary>
    private async Task<Guid> CreateDraftTalkAsync(string? title = null)
    {
        var body = new
        {
            Title = title ?? UniqueTitle("Course Item Talk"),
            Frequency = "Once",
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", body);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TalkIdDto>();
        return dto!.Id;
    }

    /// <summary>Creates a Published talk by creating a Draft then flipping its Status directly via DbContext.
    /// Mirrors the SetTargetLanguagesAsync pattern in PublishToolboxTalkTests — the CRUD endpoint used here
    /// doesn't expose Status, and driving the full publish workflow (translations, validation) is out of
    /// scope for these tests, which only care about the Draft-talk composition guard.
    /// </summary>
    private async Task<Guid> CreatePublishedTalkAsync(string? title = null)
    {
        var talkId = await CreateDraftTalkAsync(title);
        await SetTalkStatusAsync(talkId, ToolboxTalkStatus.Published);
        return talkId;
    }

    private async Task SetTalkStatusAsync(Guid talkId, ToolboxTalkStatus status)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var talk = await db.Set<ToolboxTalk>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == talkId && !t.IsDeleted);
        talk!.Status = status;
        await db.SaveChangesAsync();
    }

    private static object CreateCourseBody(string title, params Guid[] talkIds) => new
    {
        Title = title,
        Description = (string?)null,
        IsActive = true,
        RequireSequentialCompletion = true,
        RequiresRefresher = false,
        RefresherIntervalMonths = 12,
        GenerateCertificate = false,
        AutoAssignToNewEmployees = false,
        AutoAssignDueDays = 14,
        Items = talkIds.Select((id, idx) => new { ToolboxTalkId = id, OrderIndex = idx, IsRequired = true }).ToArray()
    };

    private record TalkIdDto(Guid Id);
    private record CourseIdDto(Guid Id);

    [Fact]
    public async Task CreateCourse_WithDraftTalk_ReturnsBadRequest()
    {
        // Arrange
        var draftTalkId = await CreateDraftTalkAsync();
        var body = CreateCourseBody(UniqueTitle("Draft Composition"), draftTalkId);

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/courses", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("published", "the error should be actionable and mention publication status");
    }

    [Fact]
    public async Task CreateCourse_WithOnlyPublishedTalks_ReturnsCreated()
    {
        // Arrange
        var talkId1 = await CreatePublishedTalkAsync();
        var talkId2 = await CreatePublishedTalkAsync();
        var body = CreateCourseBody(UniqueTitle("Published Composition"), talkId1, talkId2);

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/courses", body);
        var result = await response.Content.ReadFromJsonAsync<CourseIdDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UpdateCourseItems_AddingDraftTalk_ReturnsBadRequest()
    {
        // Arrange - create a course composed of one published talk
        var publishedTalkId = await CreatePublishedTalkAsync();
        var createBody = CreateCourseBody(UniqueTitle("Course For Item Update"), publishedTalkId);
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/courses", createBody);
        createResponse.EnsureSuccessStatusCode();
        var course = await createResponse.Content.ReadFromJsonAsync<CourseIdDto>();

        var draftTalkId = await CreateDraftTalkAsync();
        var updateItemsBody = new
        {
            Items = new[]
            {
                new { ToolboxTalkId = publishedTalkId, OrderIndex = 0, IsRequired = true },
                new { ToolboxTalkId = draftTalkId, OrderIndex = 1, IsRequired = true }
            }
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync($"/api/toolbox-talks/courses/{course!.Id}/items", updateItemsBody);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("published", "the error should be actionable and mention publication status");
    }

    [Fact]
    public async Task UpdateCourseItems_WithOnlyPublishedTalks_ReturnsOk()
    {
        // Arrange
        var talkId1 = await CreatePublishedTalkAsync();
        var createBody = CreateCourseBody(UniqueTitle("Course For Item Regression"), talkId1);
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/courses", createBody);
        createResponse.EnsureSuccessStatusCode();
        var course = await createResponse.Content.ReadFromJsonAsync<CourseIdDto>();

        var talkId2 = await CreatePublishedTalkAsync();
        var updateItemsBody = new
        {
            Items = new[]
            {
                new { ToolboxTalkId = talkId1, OrderIndex = 0, IsRequired = true },
                new { ToolboxTalkId = talkId2, OrderIndex = 1, IsRequired = true }
            }
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync($"/api/toolbox-talks/courses/{course!.Id}/items", updateItemsBody);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
