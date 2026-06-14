using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Core.Infrastructure.Identity;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using System.Text.Json.Serialization;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for POST /api/toolbox-talks/{talkId}/publish (Phase 5.5a).
/// Tests the new wizard Step 7 publish endpoint which operates by talkId (not sessionId).
///
/// Coverage:
///   1. Happy path — English-only talk with sections → 200, status flips to Published
///   2. Happy path — Talk with target languages and a completed validation run → 200
///   3. Rejected — Already Published → 409
///   4. Rejected — Zero sections → 409
///   5. Rejected — Target languages declared but no completed validation run → 409
///   6. Tenant isolation — Talk belongs to different tenant → 404
///   7. Auth — Operator (no Learnings.Manage) → 403
///   8. Unauthenticated → 401
/// </summary>
[Collection("Integration")]
public class PublishToolboxTalkTests : IntegrationTestBase
{
    public PublishToolboxTalkTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── local response types ─────────────────────────────────────────────────

    private record PublishTalkResult(
        [property: JsonPropertyName("talkId")] Guid TalkId,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("publishedAt")] DateTime PublishedAt);

    private record TalkIdDto(
        [property: JsonPropertyName("id")] Guid Id);

    // ── helpers ───────────────────────────────────────────────────────────────

    private static string UniqueTitle() => $"Publish Test {Guid.NewGuid():N}"[..40];

    /// <summary>Creates a draft talk with one section via the old CRUD endpoint.
    /// TargetLanguageCodes is NOT set here — use SetTargetLanguagesAsync to add it.
    /// </summary>
    private async Task<Guid> CreateTalkWithSectionsAsync()
    {
        var body = new
        {
            Title = UniqueTitle(),
            Frequency = "Once",
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Safety content.</p>", RequiresAcknowledgment = true }
            }
        };
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", body);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TalkIdDto>();
        return dto!.Id;
    }

    /// <summary>Creates a draft talk with NO sections via the initialise endpoint.
    /// Requires at least one target language to pass the initialise validator.
    /// The zero-sections gate in publish fires before the translation gate.
    /// </summary>
    private async Task<Guid> CreateTalkWithoutSectionsAsync()
    {
        var body = new
        {
            Title = UniqueTitle(),
            InputMode = "Text",
            SourceLanguageCode = "en",
            SourceText = "Some source content.",
            TargetLanguageCodes = new[] { "fr" },
            AudienceRole = "Operator",
            PreserveSourceWording = false,
            IncludeQuiz = false,
        };
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/initialise", body);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TalkIdDto>();
        return dto!.Id;
    }

    /// <summary>Patches TargetLanguageCodes on a talk directly via DbContext.
    /// The old CreateToolboxTalkCommand doesn't expose TargetLanguageCodes.
    /// </summary>
    private async Task SetTargetLanguagesAsync(Guid talkId, string languageCode)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var talk = await db.Set<ToolboxTalk>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == talkId && !t.IsDeleted);
        talk!.TargetLanguageCodes = $"""["{languageCode}"]""";
        await db.SaveChangesAsync();
    }

    /// <summary>Seeds a completed TranslationValidationRun directly in the DB.</summary>
    private async Task SeedCompletedValidationRunAsync(Guid talkId, string languageCode = "fr")
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var run = new TranslationValidationRun
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantConstants.TenantId,
            ToolboxTalkId = talkId,
            LanguageCode = languageCode,
            SectorKey = "construction",
            Status = ValidationRunStatus.Completed,
            PassThreshold = 75,
            TotalSections = 1,
            PassedSections = 1,
            ReviewSections = 0,
            FailedSections = 0,
            OverallScore = 90,
            OverallOutcome = ValidationOutcome.Pass,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Set<TranslationValidationRun>().Add(run);
        await db.SaveChangesAsync();
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    // 1 — English-only happy path
    [Fact]
    public async Task PublishByTalkId_EnglishOnly_WithSections_Returns200AndFlipsStatus()
    {
        // Arrange — talk with one section, no target languages
        var talkId = await CreateTalkWithSectionsAsync();

        // Act
        var response = await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/publish", new { });

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PublishTalkResult>();
        result.Should().NotBeNull();
        result!.TalkId.Should().Be(talkId);
        result.Status.Should().Be("Published");
        result.PublishedAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));

        // Verify DB state
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var talk = await db.Set<ToolboxTalk>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == talkId && !t.IsDeleted);
        talk.Should().NotBeNull();
        talk!.Status.Should().Be(ToolboxTalkStatus.Published);
        talk.PublishedAt.Should().NotBeNull();
    }

    // 2 — Target languages + completed validation run
    [Fact]
    public async Task PublishByTalkId_WithTargetLangsAndCompletedRun_Returns200()
    {
        // Arrange — talk with sections; set target lang "fr"; seed a completed French run
        var talkId = await CreateTalkWithSectionsAsync();
        await SetTargetLanguagesAsync(talkId, "fr");
        await SeedCompletedValidationRunAsync(talkId, "fr");

        // Act
        var response = await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/publish", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PublishTalkResult>();
        result!.Status.Should().Be("Published");
    }

    // 3 — Already published → 409
    [Fact]
    public async Task PublishByTalkId_AlreadyPublished_Returns409()
    {
        // Arrange — publish once, then try a second time
        var talkId = await CreateTalkWithSectionsAsync();
        var first = await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/publish", new { });
        first.StatusCode.Should().Be(HttpStatusCode.OK, "first publish should succeed");

        // Act — second publish attempt on same talk
        var response = await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/publish", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // 4 — Zero sections → 409 (gate fires before translation check)
    [Fact]
    public async Task PublishByTalkId_ZeroSections_Returns409()
    {
        // Arrange — talk created by initialise (wizard shell, no sections)
        var talkId = await CreateTalkWithoutSectionsAsync();

        // Act
        var response = await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/publish", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // 5 — Target languages declared but no completed validation run → 409
    [Fact]
    public async Task PublishByTalkId_WithTargetLangsAndNoCompletedRun_Returns409()
    {
        // Arrange — talk with sections and target lang "de"; no validation run seeded
        var talkId = await CreateTalkWithSectionsAsync();
        await SetTargetLanguagesAsync(talkId, "de");

        // Act
        var response = await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/publish", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // 6 — Tenant isolation: Tenant B admin cannot publish Tenant A's talk
    [Fact]
    public async Task PublishByTalkId_TalkBelongsToDifferentTenant_Returns404()
    {
        // Arrange — create talk under Tenant A (AdminClient)
        var talkId = await CreateTalkWithSectionsAsync();

        // Create Tenant B admin client with full permissions
        var tenantBClient = Factory.CreateAuthenticatedClient(
            TestTenantConstants.TenantB.Users.Admin.Id,
            TestTenantConstants.TenantB.Users.Admin.Email,
            TestTenantConstants.TenantB.TenantId,
            new[] { "Admin" },
            Permissions.GetAll());

        // Act — Tenant B tries to publish Tenant A's talk
        var response = await tenantBClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/publish", new { });

        // Assert — tenant query filter hides the talk → 404
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 7 — Operator (no Learnings.Manage permission) → 403
    [Fact]
    public async Task PublishByTalkId_OperatorWithoutManagePermission_Returns403()
    {
        // Arrange
        var talkId = await CreateTalkWithSectionsAsync();

        // Act — OperatorClient has Learnings.View only
        var response = await OperatorClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/publish", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // 8 — Unauthenticated → 401
    [Fact]
    public async Task PublishByTalkId_Unauthenticated_Returns401()
    {
        // Arrange
        var talkId = Guid.NewGuid();

        // Act
        var response = await UnauthenticatedClient.PostAsJsonAsync(
            $"/api/toolbox-talks/{talkId}/publish", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
