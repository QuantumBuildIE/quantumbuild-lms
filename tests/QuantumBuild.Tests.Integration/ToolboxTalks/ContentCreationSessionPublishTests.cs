using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for the legacy Create-wizard's session-based publish flow
/// (POST /api/toolbox-talks/create/session/{id}/publish).
///
/// Covers the cover-image persistence fix documented in
/// docs/image-removed-on-percentage-recon.md: ContentCreationSessionService.PublishAsLessonAsync
/// copied every SessionSettingsDto behaviour field (IsActiveOnPublish, GenerateCertificate,
/// MinimumWatchPercent, AutoAssign, AutoAssignDueDays) onto the published ToolboxTalk except
/// CoverImageUrl, so every legacy-wizard publish silently dropped the cover image.
///
/// Also covers the adjacent course-path gap: PublishAsCourseAsync never synced
/// IsActiveOnPublish/GenerateCertificate/AutoAssign/AutoAssignDueDays onto the ToolboxTalkCourse
/// entity (these four fields already exist on ToolboxTalkCourse and gate course-level certificate
/// issuance, auto-assignment, and assignability elsewhere) — it hardcoded IsActive = true and left
/// the rest at entity defaults.
/// </summary>
[Collection("Integration")]
public class ContentCreationSessionPublishTests : IntegrationTestBase
{
    public ContentCreationSessionPublishTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── local response types ─────────────────────────────────────────────────

    private record SessionDto(
        Guid Id,
        [property: JsonConverter(typeof(JsonStringEnumConverter))]
        ContentCreationSessionStatus Status);

    private record PublishResultDto(
        bool Success,
        Guid? OutputId,
        [property: JsonConverter(typeof(JsonStringEnumConverter))]
        OutputType? OutputType,
        string? ErrorMessage);

    private record SessionSettingsResponseDto(string? CoverImageUrl);

    // ── helpers ───────────────────────────────────────────────────────────────

    private static string UniqueTitle(string prefix = "Legacy Publish Test") =>
        $"{prefix} {Guid.NewGuid():N}"[..Math.Min(80, prefix.Length + 33)];

    private static byte[] CreateMinimalPngBytes() =>
        Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    /// <summary>Creates a Text-mode session and parses it with 2 sections, so the session lands
    /// in Parsed status with OutputType = Lesson (SuggestOutputType requires 3+ sections to
    /// suggest Course). Sets NextSections explicitly — FakeContentParserService is a shared
    /// singleton and other tests in this class mutate it for the Course path.</summary>
    private async Task<Guid> CreateAndParseSessionAsync()
    {
        Factory.FakeContentParserService.NextSections =
        [
            new("Section 1: Introduction", "<p>Introduction content.</p>", 1),
            new("Section 2: Key Points", "<p>Key points content.</p>", 2),
        ];

        var createBody = new
        {
            InputMode = "Text",
            SourceText = "Safety content used for the legacy publish cover-image regression test.",
            IncludeQuiz = false,
            AudienceRole = "Operator",
            PreserveSourceWording = false,
        };
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/create/session", createBody);
        createResponse.EnsureSuccessStatusCode();
        var session = await createResponse.Content.ReadFromJsonAsync<SessionDto>()
                      ?? throw new InvalidOperationException("Create session returned null");

        var parseResponse = await AdminClient.PostAsync(
            $"/api/toolbox-talks/create/session/{session.Id}/parse", content: null);
        parseResponse.EnsureSuccessStatusCode();

        return session.Id;
    }

    private async Task<string> UploadSessionCoverImageAsync(Guid sessionId)
    {
        using var content = new MultipartFormDataContent();
        var imageBytes = CreateMinimalPngBytes();
        using var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "file", "cover.png");

        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/create/session/{sessionId}/cover-image", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var settingsResponse = await AdminClient.GetAsync(
            $"/api/toolbox-talks/create/session/{sessionId}/settings");
        settingsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var settings = await settingsResponse.Content.ReadFromJsonAsync<SessionSettingsResponseDto>();
        settings!.CoverImageUrl.Should().NotBeNullOrEmpty("upload must have set the session's cover image");
        return settings.CoverImageUrl!;
    }

    private async Task<ToolboxTalk?> GetTalkAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<ToolboxTalk>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
    }

    private async Task<ToolboxTalkCourse?> GetCourseAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<ToolboxTalkCourse>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
    }

    /// <summary>Creates a Text-mode session and parses it with 3 sections, so
    /// FakeContentParserService.SuggestOutputType lands the session on OutputType = Course.</summary>
    private async Task<Guid> CreateAndParseCourseSessionAsync()
    {
        Factory.FakeContentParserService.NextSections =
        [
            new("Section 1: Introduction", "<p>Introduction content.</p>", 1),
            new("Section 2: Key Points", "<p>Key points content.</p>", 2),
            new("Section 3: Wrap Up", "<p>Wrap up content.</p>", 3),
        ];

        var createBody = new
        {
            InputMode = "Text",
            SourceText = "Safety content used for the course-publish behaviour-field sync regression test.",
            IncludeQuiz = false,
            AudienceRole = "Operator",
            PreserveSourceWording = false,
        };
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/create/session", createBody);
        createResponse.EnsureSuccessStatusCode();
        var session = await createResponse.Content.ReadFromJsonAsync<SessionDto>()
                      ?? throw new InvalidOperationException("Create session returned null");

        var parseResponse = await AdminClient.PostAsync(
            $"/api/toolbox-talks/create/session/{session.Id}/parse", content: null);
        parseResponse.EnsureSuccessStatusCode();

        return session.Id;
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    // 1 — Session cover image is persisted to ToolboxTalk.CoverImageUrl on publish (no prior draft talk)
    [Fact]
    public async Task Publish_WithSessionCoverImage_PersistsCoverImageUrlOnTalk()
    {
        var sessionId = await CreateAndParseSessionAsync();
        var coverImageUrl = await UploadSessionCoverImageAsync(sessionId);

        var publishResponse = await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/create/session/{sessionId}/publish",
            new { Title = UniqueTitle() });

        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await publishResponse.Content.ReadFromJsonAsync<PublishResultDto>();
        result!.Success.Should().BeTrue();
        result.OutputId.Should().NotBeNull();

        var talk = await GetTalkAsync(result.OutputId!.Value);
        talk.Should().NotBeNull();
        talk!.CoverImageUrl.Should().Be(coverImageUrl,
            "the cover image uploaded during the session must survive publish onto the ToolboxTalk entity");
    }

    // 2 — Publish without ever uploading a cover image leaves CoverImageUrl null (no false positive)
    [Fact]
    public async Task Publish_WithoutCoverImage_LeavesCoverImageUrlNull()
    {
        var sessionId = await CreateAndParseSessionAsync();

        var publishResponse = await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/create/session/{sessionId}/publish",
            new { Title = UniqueTitle() });

        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await publishResponse.Content.ReadFromJsonAsync<PublishResultDto>();
        result!.Success.Should().BeTrue();

        var talk = await GetTalkAsync(result.OutputId!.Value);
        talk!.CoverImageUrl.Should().BeNull();
    }

    // 3 — Course publish syncs IsActive/GenerateCertificate/AutoAssign/AutoAssignDueDays from
    // session settings onto the ToolboxTalkCourse entity. Prior to this fix, PublishAsCourseAsync
    // hardcoded IsActive = true and left the other three fields at their entity defaults regardless
    // of what the user configured in the Settings step — this is the course-path counterpart of the
    // cover-image gap covered by the tests above.
    [Fact]
    public async Task PublishCourse_WithSessionSettings_SyncsBehaviourFieldsOntoCourse()
    {
        var sessionId = await CreateAndParseCourseSessionAsync();

        var settingsBody = new
        {
            Title = "",
            Description = "",
            CoverImageUrl = (string?)null,
            Category = (string?)null,
            RefresherFrequency = "Once",
            IsActiveOnPublish = false,
            GenerateCertificate = false,
            MinimumWatchPercent = 90,
            AutoAssign = true,
            AutoAssignDueDays = 30,
            GenerateSlideshow = false,
            SlideshowSource = "none",
        };
        var settingsResponse = await AdminClient.PutAsJsonAsync(
            $"/api/toolbox-talks/create/session/{sessionId}/settings", settingsBody);
        settingsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var publishResponse = await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/create/session/{sessionId}/publish",
            new { Title = UniqueTitle("Course Publish Test") });

        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await publishResponse.Content.ReadFromJsonAsync<PublishResultDto>();
        result!.Success.Should().BeTrue();
        result.OutputType.Should().Be(OutputType.Course);
        result.OutputId.Should().NotBeNull();

        var course = await GetCourseAsync(result.OutputId!.Value);
        course.Should().NotBeNull();
        course!.IsActive.Should().BeFalse(
            "IsActiveOnPublish=false in session settings must flow onto the course instead of the previously hardcoded true");
        course.GenerateCertificate.Should().BeFalse();
        course.AutoAssignToNewEmployees.Should().BeTrue();
        course.AutoAssignDueDays.Should().Be(30);
    }
}
