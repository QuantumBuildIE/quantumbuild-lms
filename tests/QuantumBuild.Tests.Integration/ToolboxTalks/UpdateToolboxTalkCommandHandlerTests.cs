using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using System.Text.Json.Serialization;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for the staleness detection and MarkStale wiring in
/// UpdateToolboxTalkCommandHandler (Phase 3b.1 §2.1–2.3).
///
/// Dispatch strategy: AdminClient (HTTP client authenticated as TestTenant Admin).
/// The handler uses the tenant query filter for the ToolboxTalk lookup, so HTTP
/// context with the correct TenantId is required.
///
/// Workflow events are stamped with TenantId = TestTenantConstants.TenantId (HTTP
/// context). Assertions use IgnoreQueryFilters() to find them from the non-HTTP
/// test scope. ToolboxTalkTranslation rows are seeded with an explicit TenantId
/// so the handler's query filter finds them.
///
/// Isolation: each test creates a talk with a unique title so the per-tenant title
/// uniqueness check in UpdateToolboxTalkCommandHandler never fires across tests.
/// Helper methods use self-contained scopes (not GetDbContext()) to avoid leaking
/// DB connections.
/// </summary>
[Collection("Integration")]
public class UpdateToolboxTalkCommandHandlerTests : IntegrationTestBase
{
    public UpdateToolboxTalkCommandHandlerTests(CustomWebApplicationFactory factory)
        : base(factory) { }

    // ── helpers ────────────────────────────────────────────────────────────────

    /// <summary>Generates a title that is unique within this test run.</summary>
    private static string UniqueTitle(string prefix) =>
        $"{prefix} {Guid.NewGuid():N}"[..Math.Min(60, prefix.Length + 33)];

    /// <summary>Creates a talk via AdminClient and returns its DTO.</summary>
    private async Task<TalkResponseDto> CreateTalkAsync(string title, params SectionCreateDto[] sections)
    {
        var body = new
        {
            Title = title,
            Description = (string?)null,
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = sections
        };
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", body);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TalkResponseDto>()
               ?? throw new InvalidOperationException("Create talk returned null");
    }

    /// <summary>
    /// Seeds a ToolboxTalkTranslation row with TenantId = TestTenantConstants.TenantId
    /// so the handler's tenant-scoped query finds it. Uses a self-contained scope to
    /// avoid leaking DB connections via GetDbContext().
    /// </summary>
    private async Task SeedTranslationAsync(Guid talkId, string languageCode, bool needsRevalidation = false)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<ToolboxTalkTranslation>().Add(new ToolboxTalkTranslation
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantConstants.TenantId, // explicit — survives SetAuditFields auto-stamp
            ToolboxTalkId = talkId,
            LanguageCode = languageCode,
            TranslatedTitle = $"[{languageCode}] placeholder title",
            TranslatedSections = "[]",
            EmailSubject = string.Empty,
            EmailBody = string.Empty,
            TranslationProvider = "Test",
            TranslatedAt = DateTime.UtcNow,
            NeedsRevalidation = needsRevalidation
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Queries MarkedStale workflow events for a specific talk + language pair.
    /// Uses IgnoreQueryFilters because events are stamped with TestTenantConstants.TenantId
    /// (HTTP context), but the test-scope DbContext resolves with TenantId = Guid.Empty.
    /// Uses a self-contained scope to avoid leaking DB connections.
    /// </summary>
    private async Task<List<WorkflowEvent>> GetMarkedStaleEventsAsync(Guid talkId, string languageCode)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.TargetEntityId == talkId
                     && e.TargetEntitySubKey == languageCode
                     && e.EventType == WorkflowEventTypes.MarkedStale
                     && !e.IsDeleted)
            .ToListAsync();
    }

    /// <summary>
    /// Queries the translation row, bypassing query filters (same TenantId mismatch as events).
    /// Uses a self-contained scope to avoid leaking DB connections.
    /// </summary>
    private async Task<ToolboxTalkTranslation?> GetTranslationAsync(Guid talkId, string languageCode)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<ToolboxTalkTranslation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.ToolboxTalkId == talkId
                                   && t.LanguageCode == languageCode
                                   && !t.IsDeleted);
    }

    /// <summary>
    /// Submits a PUT request for the given talk. Returns the HTTP response.
    /// </summary>
    private Task<HttpResponseMessage> PutTalkAsync(
        Guid talkId,
        string title,
        string? description,
        IEnumerable<object> sections,
        IEnumerable<object>? questions = null) =>
        AdminClient.PutAsJsonAsync($"/api/toolbox-talks/{talkId}", new
        {
            Id = talkId,
            Title = title,
            Description = description,
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = sections,
            Questions = questions ?? Array.Empty<object>()
        });

    // ── tests ──────────────────────────────────────────────────────────────────

    // 1 — Title changed → all translated languages get MarkedStale + NeedsRevalidation=true
    [Fact]
    public async Task TitleChange_MarksAllTranslationsStale()
    {
        // Unique titles prevent the per-tenant title uniqueness check from conflicting
        // with other tests that also change the title.
        var originalTitle = UniqueTitle("OriginalTitle");
        var changedTitle = UniqueTitle("ChangedTitle");

        // Arrange
        var talk = await CreateTalkAsync(originalTitle,
            new SectionCreateDto(1, "Section", "<p>Content</p>", true));
        await SeedTranslationAsync(talk.Id, "fr");
        await SeedTranslationAsync(talk.Id, "de");

        // Act — change only the title
        var response = await PutTalkAsync(
            talk.Id, changedTitle, null,
            talk.Sections.Select(s => (object)new { s.Id, s.SectionNumber, s.Title, s.Content, s.RequiresAcknowledgment }));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — MarkedStale event written for both languages
        var frEvents = await GetMarkedStaleEventsAsync(talk.Id, "fr");
        var deEvents = await GetMarkedStaleEventsAsync(talk.Id, "de");
        frEvents.Should().NotBeEmpty("title change should trigger MarkStale for French");
        deEvents.Should().NotBeEmpty("title change should trigger MarkStale for German");

        // NeedsRevalidation flag set on both translation rows
        var frTranslation = await GetTranslationAsync(talk.Id, "fr");
        var deTranslation = await GetTranslationAsync(talk.Id, "de");
        frTranslation!.NeedsRevalidation.Should().BeTrue();
        deTranslation!.NeedsRevalidation.Should().BeTrue();
    }

    // 2 — Description changed → MarkedStale
    [Fact]
    public async Task DescriptionChange_MarksTranslationsStale()
    {
        var talkTitle = UniqueTitle("DescChangeTitle");

        // Arrange
        var talk = await CreateTalkAsync(talkTitle,
            new SectionCreateDto(1, "Section", "<p>Content</p>", true));
        await SeedTranslationAsync(talk.Id, "fr");

        // Act — change only the description (title stays the same, no uniqueness conflict)
        var response = await PutTalkAsync(
            talk.Id, talk.Title, "Changed description",
            talk.Sections.Select(s => (object)new { s.Id, s.SectionNumber, s.Title, s.Content, s.RequiresAcknowledgment }));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert
        var events = await GetMarkedStaleEventsAsync(talk.Id, "fr");
        events.Should().NotBeEmpty("description change should trigger MarkStale");
    }

    // 3 — Section content edited → MarkedStale + NeedsRevalidation=true
    [Fact]
    public async Task SectionContentEdit_MarksTranslationsStale()
    {
        var talkTitle = UniqueTitle("SectionEditTitle");

        // Arrange
        var talk = await CreateTalkAsync(talkTitle,
            new SectionCreateDto(1, "Section", "<p>Original content</p>", true));
        await SeedTranslationAsync(talk.Id, "fr");

        // Act — same section ID, different Content
        var response = await PutTalkAsync(
            talk.Id, talk.Title, null,
            new object[]
            {
                new { talk.Sections[0].Id, talk.Sections[0].SectionNumber, talk.Sections[0].Title,
                      Content = "<p>Changed content</p>", talk.Sections[0].RequiresAcknowledgment }
            });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert
        var events = await GetMarkedStaleEventsAsync(talk.Id, "fr");
        events.Should().NotBeEmpty("section content change should trigger MarkStale");

        var translation = await GetTranslationAsync(talk.Id, "fr");
        translation!.NeedsRevalidation.Should().BeTrue();
    }

    // 4 — New section added → MarkedStale
    [Fact]
    public async Task SectionAdded_MarksTranslationsStale()
    {
        var talkTitle = UniqueTitle("SectionAddedTitle");

        // Arrange
        var talk = await CreateTalkAsync(talkTitle,
            new SectionCreateDto(1, "Section 1", "<p>Content</p>", true));
        await SeedTranslationAsync(talk.Id, "fr");

        // Act — keep original section + add a new one (no Id → treated as new)
        var response = await PutTalkAsync(
            talk.Id, talk.Title, null,
            new object[]
            {
                new { talk.Sections[0].Id, talk.Sections[0].SectionNumber, talk.Sections[0].Title,
                      talk.Sections[0].Content, talk.Sections[0].RequiresAcknowledgment },
                new { SectionNumber = 2, Title = "New Section", Content = "<p>New</p>", RequiresAcknowledgment = true }
            });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert
        var events = await GetMarkedStaleEventsAsync(talk.Id, "fr");
        events.Should().NotBeEmpty("adding a section should trigger MarkStale");
    }

    // 5 — Section removed → MarkedStale
    [Fact]
    public async Task SectionRemoved_MarksTranslationsStale()
    {
        var talkTitle = UniqueTitle("SectionRemovedTitle");

        // Arrange — create talk with two sections
        var talk = await CreateTalkAsync(talkTitle,
            new SectionCreateDto(1, "Section 1", "<p>Content 1</p>", true),
            new SectionCreateDto(2, "Section 2", "<p>Content 2</p>", true));
        await SeedTranslationAsync(talk.Id, "fr");

        // Act — only send the first section (second is omitted → soft-deleted by handler)
        var response = await PutTalkAsync(
            talk.Id, talk.Title, null,
            new object[]
            {
                new { talk.Sections[0].Id, talk.Sections[0].SectionNumber, talk.Sections[0].Title,
                      talk.Sections[0].Content, talk.Sections[0].RequiresAcknowledgment }
            });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert
        var events = await GetMarkedStaleEventsAsync(talk.Id, "fr");
        events.Should().NotBeEmpty("removing a section should trigger MarkStale");
    }

    // 6 — Pure reorder (same IDs, same Title/Content, different SectionNumber) → no MarkStale
    [Fact]
    public async Task PureReorder_DoesNotMarkStale()
    {
        var talkTitle = UniqueTitle("PureReorderTitle");

        // Arrange — create talk with two sections
        var talk = await CreateTalkAsync(talkTitle,
            new SectionCreateDto(1, "Section 1", "<p>Content 1</p>", true),
            new SectionCreateDto(2, "Section 2", "<p>Content 2</p>", true));
        await SeedTranslationAsync(talk.Id, "fr");

        // Act — swap SectionNumber values but keep IDs and content identical
        var response = await PutTalkAsync(
            talk.Id, talk.Title, null,
            new object[]
            {
                new { talk.Sections[0].Id, SectionNumber = 2, talk.Sections[0].Title,
                      talk.Sections[0].Content, talk.Sections[0].RequiresAcknowledgment },
                new { talk.Sections[1].Id, SectionNumber = 1, talk.Sections[1].Title,
                      talk.Sections[1].Content, talk.Sections[1].RequiresAcknowledgment }
            });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — no MarkedStale event should exist
        var events = await GetMarkedStaleEventsAsync(talk.Id, "fr");
        events.Should().BeEmpty("pure reorder must not trigger MarkStale");
    }

    // 7 — No stalening change at all (identical PUT) → no MarkStale events
    [Fact]
    public async Task NoChange_NoMarkStaleEvents()
    {
        var talkTitle = UniqueTitle("NoChangeTitle");

        // Arrange
        var talk = await CreateTalkAsync(talkTitle,
            new SectionCreateDto(1, "Section", "<p>Content</p>", true));
        await SeedTranslationAsync(talk.Id, "fr");

        // Act — PUT with identical data (same title, description, sections)
        var response = await PutTalkAsync(
            talk.Id, talk.Title, null,
            talk.Sections.Select(s => (object)new { s.Id, s.SectionNumber, s.Title, s.Content, s.RequiresAcknowledgment }));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — nothing stale
        var events = await GetMarkedStaleEventsAsync(talk.Id, "fr");
        events.Should().BeEmpty("identical PUT must not trigger MarkStale");

        var translation = await GetTranslationAsync(talk.Id, "fr");
        translation!.NeedsRevalidation.Should().BeFalse();
    }

    // 8 — Talk with no translations → stalening change fires but MarkStale never called (nothing to stale)
    [Fact]
    public async Task TitleChange_NoTranslations_NoMarkStaleEvents()
    {
        var originalTitle = UniqueTitle("NoTransOriginal");
        var changedTitle = UniqueTitle("NoTransChanged");

        // Arrange — talk with no translation rows seeded
        var talk = await CreateTalkAsync(originalTitle,
            new SectionCreateDto(1, "Section", "<p>Content</p>", true));

        // Act — change title
        var response = await PutTalkAsync(
            talk.Id, changedTitle, null,
            talk.Sections.Select(s => (object)new { s.Id, s.SectionNumber, s.Title, s.Content, s.RequiresAcknowledgment }));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — no MarkedStale events exist for this talk at all
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var allEvents = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.TargetEntityId == talk.Id
                     && e.EventType == WorkflowEventTypes.MarkedStale
                     && !e.IsDeleted)
            .ToListAsync();
        allEvents.Should().BeEmpty("no translations exist so MarkStale should never be called");
    }

    // 9 — Multiple translations → both get MarkedStale when section content changes
    [Fact]
    public async Task MultipleTranslations_AllMarkedStaleOnSectionChange()
    {
        var talkTitle = UniqueTitle("MultiTransTitle");

        // Arrange
        var talk = await CreateTalkAsync(talkTitle,
            new SectionCreateDto(1, "Section", "<p>Original</p>", true));
        await SeedTranslationAsync(talk.Id, "fr");
        await SeedTranslationAsync(talk.Id, "de");

        // Act — edit section content
        var response = await PutTalkAsync(
            talk.Id, talk.Title, null,
            new object[]
            {
                new { talk.Sections[0].Id, talk.Sections[0].SectionNumber, talk.Sections[0].Title,
                      Content = "<p>Modified content</p>", talk.Sections[0].RequiresAcknowledgment }
            });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — both languages staled
        var frEvents = await GetMarkedStaleEventsAsync(talk.Id, "fr");
        var deEvents = await GetMarkedStaleEventsAsync(talk.Id, "de");
        frEvents.Should().NotBeEmpty("French should be marked stale");
        deEvents.Should().NotBeEmpty("German should be marked stale");

        var frTranslation = await GetTranslationAsync(talk.Id, "fr");
        var deTranslation = await GetTranslationAsync(talk.Id, "de");
        frTranslation!.NeedsRevalidation.Should().BeTrue();
        deTranslation!.NeedsRevalidation.Should().BeTrue();
    }

    // 10 — Explicit RefresherIntervalMonths honored over Frequency mapper
    //      Regression guard: before the fix, Frequency=Monthly would silently overwrite
    //      RefresherIntervalMonths=3 (quarterly) with 1 (monthly).
    [Fact]
    public async Task ExplicitRefresherFields_HonoredOverFrequencyMapper()
    {
        var talkTitle = UniqueTitle("ExplicitRefresher");

        // Arrange — create a talk with default refresher settings
        var talk = await CreateTalkAsync(talkTitle,
            new SectionCreateDto(1, "Section", "<p>Content</p>", true));

        // Act — PUT with explicit quarterly refresher (3 months) but Frequency=Monthly
        // (Monthly is the nearest legacy bucket for quarterly, but must NOT override the explicit 3).
        var response = await AdminClient.PutAsJsonAsync($"/api/toolbox-talks/{talk.Id}", new
        {
            Id = talk.Id,
            Title = talk.Title,
            Frequency = ToolboxTalkFrequency.Monthly,   // legacy bucket (nearest for quarterly)
            RequiresRefresher = true,
            RefresherIntervalMonths = 3,                // explicit quarterly value
            RequiresQuiz = false,
            IsActive = true,
            Sections = talk.Sections.Select(s => new { s.Id, s.SectionNumber, s.Title, s.Content, s.RequiresAcknowledgment }),
            Questions = Array.Empty<object>()
        });
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        // Assert — DB must have the explicit 3-month interval, NOT the mapper's 1-month output
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbTalk = await db.Set<QuantumBuild.Modules.ToolboxTalks.Domain.Entities.ToolboxTalk>()
            .IgnoreQueryFilters()
            .FirstAsync(t => t.Id == talk.Id);
        dbTalk.RequiresRefresher.Should().BeTrue();
        dbTalk.RefresherIntervalMonths.Should().Be(3, "explicit quarterly value must survive the Frequency=Monthly mapper");
    }

    // ── local DTOs (private to this test class) ────────────────────────────────

    private record SectionCreateDto(
        int SectionNumber,
        string Title,
        string Content,
        bool RequiresAcknowledgment);

    private record TalkResponseDto(
        Guid Id,
        string Title,
        string? Description,
        [property: JsonConverter(typeof(JsonStringEnumConverter))]
        ToolboxTalkFrequency Frequency,
        bool RequiresQuiz,
        bool IsActive,
        SectionResponseDto[] Sections);

    private record SectionResponseDto(
        Guid Id,
        int SectionNumber,
        string Title,
        string Content,
        bool RequiresAcknowledgment);
}
