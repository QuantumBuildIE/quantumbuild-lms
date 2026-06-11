using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Tests.Common.TestTenant;
using System.Net;
using System.Text.Json.Serialization;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for PUT /api/toolbox-talks/{id}/settings (Phase 5.3d Step 4).
/// Tests UpdateToolboxTalkSettingsCommandHandler: field persistence, status guard,
/// title uniqueness, stalening wiring, and cover-image endpoints.
///
/// Dispatch: AdminClient (Learnings.Manage) for success paths.
///           OperatorClient (no Learnings.Manage) for 403 checks.
///           UnauthenticatedClient for 401 checks.
///
/// Talk creation uses POST /api/toolbox-talks/initialise (wizard Step 1) so the
/// talk starts as a Draft — the only status accepted by the settings handler.
/// </summary>
[Collection("Integration")]
public class UpdateToolboxTalkSettingsCommandHandlerTests : IntegrationTestBase
{
    public UpdateToolboxTalkSettingsCommandHandlerTests(CustomWebApplicationFactory factory)
        : base(factory) { }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string UniqueTitle(string prefix = "Settings Test") =>
        $"{prefix} {Guid.NewGuid():N}"[..Math.Min(80, prefix.Length + 33)];

    private static object MinimalInitRequest(string title) => new
    {
        Title = title,
        InputMode = "Text",
        SourceLanguageCode = "en",
        SourceText = "Safety content for testing settings step.",
        TargetLanguageCodes = new[] { "fr" },
        AudienceRole = "Operator",
        PreserveSourceWording = false,
        IncludeQuiz = true,
    };

    private async Task<InitialisedTalkDto> InitialiseAsync(string? title = null)
    {
        var t = title ?? UniqueTitle();
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/initialise", MinimalInitRequest(t));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InitialisedTalkDto>()
               ?? throw new InvalidOperationException("Initialise returned null");
    }

    private static object DefaultSettingsBody(string title) => new
    {
        Title = title,
        Description = (string?)null,
        Category = (string?)null,
        RefresherFrequency = "Once",
        IsActive = true,
        GenerateCertificate = false,
        MinimumVideoWatchPercent = 90,
        AutoAssignToNewEmployees = false,
        AutoAssignDueDays = 30,
        GenerateSlidesFromPdf = false,
    };

    private Task<HttpResponseMessage> PutSettingsAsync(Guid talkId, object body) =>
        AdminClient.PutAsJsonAsync($"/api/toolbox-talks/{talkId}/settings", body);

    private async Task SeedTranslationAsync(Guid talkId, string languageCode)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<ToolboxTalkTranslation>().Add(new ToolboxTalkTranslation
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantConstants.TenantId,
            ToolboxTalkId = talkId,
            LanguageCode = languageCode,
            TranslatedTitle = $"[{languageCode}] placeholder",
            TranslatedSections = "[]",
            EmailSubject = string.Empty,
            EmailBody = string.Empty,
            TranslationProvider = "Test",
            TranslatedAt = DateTime.UtcNow,
            NeedsRevalidation = false,
        });
        await db.SaveChangesAsync();
    }

    private async Task<ToolboxTalk?> GetTalkAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<ToolboxTalk>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
    }

    private async Task<ToolboxTalkTranslation?> GetTranslationAsync(Guid talkId, string lang)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<ToolboxTalkTranslation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.ToolboxTalkId == talkId && t.LanguageCode == lang && !t.IsDeleted);
    }

    private async Task<List<WorkflowEvent>> GetMarkedStaleEventsAsync(Guid talkId, string lang)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.TargetEntityId == talkId
                     && e.TargetEntitySubKey == lang
                     && e.EventType == WorkflowEventTypes.MarkedStale
                     && !e.IsDeleted)
            .ToListAsync();
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    // 1 — Happy path: all fields persisted, LastEditedStep advances to 4
    [Fact]
    public async Task HappyPath_AllFieldsPersisted_LastEditedStep4()
    {
        var talk = await InitialiseAsync();

        var body = new
        {
            Title = UniqueTitle("Updated Title"),
            Description = "Updated description",
            Category = "Safety",
            RefresherFrequency = "Monthly",
            IsActive = false,
            GenerateCertificate = true,
            MinimumVideoWatchPercent = 75,
            AutoAssignToNewEmployees = true,
            AutoAssignDueDays = 14,
            GenerateSlidesFromPdf = true,
        };

        var response = await PutSettingsAsync(talk.Id, body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SettingsTalkDto>();
        result.Should().NotBeNull();

        var dbTalk = await GetTalkAsync(talk.Id);
        dbTalk!.Title.Should().Be(body.Title);
        dbTalk.Description.Should().Be("Updated description");
        dbTalk.Category.Should().Be("Safety");
        dbTalk.RequiresRefresher.Should().BeTrue();
        dbTalk.RefresherIntervalMonths.Should().Be(1);
        dbTalk.IsActive.Should().BeFalse();
        dbTalk.GenerateCertificate.Should().BeTrue();
        dbTalk.MinimumVideoWatchPercent.Should().Be(75);
        dbTalk.AutoAssignToNewEmployees.Should().BeTrue();
        dbTalk.AutoAssignDueDays.Should().Be(14);
        dbTalk.GenerateSlidesFromPdf.Should().BeTrue();
        dbTalk.LastEditedStep.Should().Be(4);
    }

    // 2 — RefresherFrequency = Once → RequiresRefresher = false
    [Fact]
    public async Task RefresherFrequency_Once_SetsFalse()
    {
        var talk = await InitialiseAsync();

        var response = await PutSettingsAsync(talk.Id, DefaultSettingsBody(UniqueTitle("Refresher Once")));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dbTalk = await GetTalkAsync(talk.Id);
        dbTalk!.RequiresRefresher.Should().BeFalse();
    }

    // 3 — RefresherFrequency = Quarterly → interval = 3
    [Fact]
    public async Task RefresherFrequency_Quarterly_SetsInterval3()
    {
        var talk = await InitialiseAsync();
        var title = UniqueTitle("Quarterly");

        var response = await PutSettingsAsync(talk.Id, new
        {
            Title = title,
            RefresherFrequency = "Quarterly",
            IsActive = true,
            GenerateCertificate = false,
            MinimumVideoWatchPercent = 90,
            AutoAssignToNewEmployees = false,
            AutoAssignDueDays = 30,
            GenerateSlidesFromPdf = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dbTalk = await GetTalkAsync(talk.Id);
        dbTalk!.RequiresRefresher.Should().BeTrue();
        dbTalk.RefresherIntervalMonths.Should().Be(3);
    }

    // 4 — RefresherFrequency = Annually → interval = 12
    [Fact]
    public async Task RefresherFrequency_Annually_SetsInterval12()
    {
        var talk = await InitialiseAsync();
        var title = UniqueTitle("Annually");

        var response = await PutSettingsAsync(talk.Id, new
        {
            Title = title,
            RefresherFrequency = "Annually",
            IsActive = true,
            GenerateCertificate = false,
            MinimumVideoWatchPercent = 90,
            AutoAssignToNewEmployees = false,
            AutoAssignDueDays = 30,
            GenerateSlidesFromPdf = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dbTalk = await GetTalkAsync(talk.Id);
        dbTalk!.RequiresRefresher.Should().BeTrue();
        dbTalk.RefresherIntervalMonths.Should().Be(12);
    }

    // 5 — Title changed + existing translation → MarkStale fired, NeedsRevalidation = true
    [Fact]
    public async Task TitleChange_WithTranslation_MarksStaleAndSetsFlag()
    {
        var original = UniqueTitle("OriginalTitle");
        var changed = UniqueTitle("ChangedTitle");

        var talk = await InitialiseAsync(original);
        await SeedTranslationAsync(talk.Id, "fr");

        var response = await PutSettingsAsync(talk.Id, DefaultSettingsBody(changed));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = await GetMarkedStaleEventsAsync(talk.Id, "fr");
        events.Should().NotBeEmpty("title change should fire MarkStale for French");

        var translation = await GetTranslationAsync(talk.Id, "fr");
        translation!.NeedsRevalidation.Should().BeTrue();
    }

    // 6 — Title unchanged → MarkStale NOT fired
    [Fact]
    public async Task TitleUnchanged_NoTranslationMarkedStale()
    {
        var title = UniqueTitle("SameTitle");

        var talk = await InitialiseAsync(title);
        await SeedTranslationAsync(talk.Id, "fr");

        // PUT with identical title
        var response = await PutSettingsAsync(talk.Id, DefaultSettingsBody(title));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = await GetMarkedStaleEventsAsync(talk.Id, "fr");
        events.Should().BeEmpty("same title should not trigger MarkStale");
    }

    // 7 — Duplicate title (same tenant, different talk) → 409 TitleNotUnique
    [Fact]
    public async Task DuplicateTitle_Returns409TitleNotUnique()
    {
        var existingTitle = UniqueTitle("Existing");
        var otherTalk = await InitialiseAsync(existingTitle);
        _ = otherTalk; // ensure the talk exists

        var talk = await InitialiseAsync(); // second talk with a unique title

        var response = await PutSettingsAsync(talk.Id, DefaultSettingsBody(existingTitle));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("TitleNotUnique", "error code should be present in response");
    }

    // 8 — Own title (same talk) → OK (no false positive uniqueness error)
    [Fact]
    public async Task SameOwnTitle_DoesNotTriggerUniquenessError()
    {
        var title = UniqueTitle("OwnTitle");
        var talk = await InitialiseAsync(title);

        var response = await PutSettingsAsync(talk.Id, DefaultSettingsBody(title));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // 9 — Talk not found → 404
    [Fact]
    public async Task TalkNotFound_Returns404()
    {
        var response = await PutSettingsAsync(Guid.NewGuid(), DefaultSettingsBody("Ghost Title"));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 10 — Unauthenticated → 401
    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var talk = await InitialiseAsync();
        var response = await UnauthenticatedClient.PutAsJsonAsync(
            $"/api/toolbox-talks/{talk.Id}/settings", DefaultSettingsBody("Ghost"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 11 — Operator (no Learnings.Manage) → 403
    [Fact]
    public async Task OperatorClient_Returns403()
    {
        var talk = await InitialiseAsync();
        var response = await OperatorClient.PutAsJsonAsync(
            $"/api/toolbox-talks/{talk.Id}/settings", DefaultSettingsBody("Ghost"));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // 12 — Empty title → 400
    [Fact]
    public async Task EmptyTitle_Returns400()
    {
        var talk = await InitialiseAsync();
        var response = await PutSettingsAsync(talk.Id, new
        {
            Title = string.Empty,
            RefresherFrequency = "Once",
            IsActive = true,
            GenerateCertificate = false,
            MinimumVideoWatchPercent = 90,
            AutoAssignToNewEmployees = false,
            AutoAssignDueDays = 30,
            GenerateSlidesFromPdf = false,
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // 13 — MinimumVideoWatchPercent below 50 → 400
    [Fact]
    public async Task MinimumWatchPercentBelowRange_Returns400()
    {
        var talk = await InitialiseAsync();
        var response = await PutSettingsAsync(talk.Id, new
        {
            Title = UniqueTitle("WatchPct"),
            RefresherFrequency = "Once",
            IsActive = true,
            GenerateCertificate = false,
            MinimumVideoWatchPercent = 10,
            AutoAssignToNewEmployees = false,
            AutoAssignDueDays = 30,
            GenerateSlidesFromPdf = false,
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // 14 — AutoAssignDueDays = 0 → 400
    [Fact]
    public async Task AutoAssignDueDaysZero_Returns400()
    {
        var talk = await InitialiseAsync();
        var response = await PutSettingsAsync(talk.Id, new
        {
            Title = UniqueTitle("DueDays"),
            RefresherFrequency = "Once",
            IsActive = true,
            GenerateCertificate = false,
            MinimumVideoWatchPercent = 90,
            AutoAssignToNewEmployees = false,
            AutoAssignDueDays = 0,
            GenerateSlidesFromPdf = false,
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // 15 — Non-Draft talk → 409 WorkflowInvalidState
    [Fact]
    public async Task NonDraftTalk_Returns409WorkflowInvalidState()
    {
        // Create a talk and manually advance it to Published via the old CRUD endpoint
        var title = UniqueTitle("PublishedTalk");
        var createBody = new
        {
            Title = title,
            Description = (string?)null,
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = Array.Empty<object>()
        };
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createBody);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<TalkCreateDto>()
                      ?? throw new InvalidOperationException("Create returned null");

        // Force status to Published directly in DB
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbTalk = await db.Set<ToolboxTalk>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == created.Id);
        dbTalk!.Status = ToolboxTalkStatus.Published;
        await db.SaveChangesAsync();

        // Act
        var response = await PutSettingsAsync(created.Id, DefaultSettingsBody(UniqueTitle("PublishedUpdate")));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Draft", "error message should mention Draft status");
    }

    // 16 — Cover image upload on Draft → 200 with coverImageUrl
    [Fact]
    public async Task CoverImage_UploadOnDraft_Returns200()
    {
        var talk = await InitialiseAsync();

        using var content = new MultipartFormDataContent();
        var imageBytes = CreateMinimalPngBytes();
        using var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "file", "cover.png");

        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{talk.Id}/cover-image", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // 17 — Cover image upload wrong type → 400
    [Fact]
    public async Task CoverImage_WrongType_Returns400()
    {
        var talk = await InitialiseAsync();

        using var content = new MultipartFormDataContent();
        using var pdfContent = new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF
        pdfContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(pdfContent, "file", "doc.pdf");

        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{talk.Id}/cover-image", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // 18 — Cover image delete on Draft (no image) → 200, coverImageUrl = null
    [Fact]
    public async Task CoverImage_DeleteWhenNoneSet_Returns200Null()
    {
        var talk = await InitialiseAsync();

        var response = await AdminClient.DeleteAsync(
            $"/api/toolbox-talks/{talk.Id}/cover-image");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CoverImageResponseDto>();
        body!.CoverImageUrl.Should().BeNull();
    }

    // ── helpers for cover image tests ─────────────────────────────────────────

    private static byte[] CreateMinimalPngBytes()
    {
        // Minimal valid 1×1 transparent PNG (89 bytes)
        return Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
    }

    // ── local DTOs ─────────────────────────────────────────────────────────────

    private record InitialisedTalkDto(
        Guid Id,
        string Title,
        [property: JsonConverter(typeof(JsonStringEnumConverter))]
        ToolboxTalkStatus Status,
        bool IsActive,
        int? LastEditedStep
    );

    private record SettingsTalkDto(
        Guid Id,
        string Title,
        string? Description,
        string? Category,
        bool RequiresRefresher,
        int RefresherIntervalMonths,
        bool IsActive,
        bool GenerateCertificate,
        int MinimumVideoWatchPercent,
        bool AutoAssignToNewEmployees,
        int AutoAssignDueDays,
        bool GenerateSlidesFromPdf,
        int? LastEditedStep,
        string? CoverImageUrl
    );

    private record TalkCreateDto(
        Guid Id,
        string Title,
        [property: JsonConverter(typeof(JsonStringEnumConverter))]
        ToolboxTalkStatus Status
    );

    private record CoverImageResponseDto(string? CoverImageUrl);
}
