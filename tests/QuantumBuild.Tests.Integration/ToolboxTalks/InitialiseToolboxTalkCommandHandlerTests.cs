using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using System.Net;
using System.Text.Json.Serialization;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for POST /api/toolbox-talks/initialise (Phase 5.3a Step 1).
/// Tests the InitialiseToolboxTalkCommandHandler — creates a Draft ToolboxTalk shell
/// with no sections or questions, ready for wizard steps 2–8 to build on.
///
/// Dispatch: AdminClient (Learnings.Manage permission) for success paths.
///           OperatorClient (no Learnings.Manage) for 403 checks.
///           UnauthenticatedClient for 401 checks.
///
/// Isolation: each test uses a unique title via UniqueTitle() to avoid the per-tenant
/// title uniqueness check from conflicting across test methods.
/// </summary>
[Collection("Integration")]
public class InitialiseToolboxTalkCommandHandlerTests : IntegrationTestBase
{
    public InitialiseToolboxTalkCommandHandlerTests(CustomWebApplicationFactory factory)
        : base(factory) { }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string UniqueTitle(string prefix = "Test Learning") =>
        $"{prefix} {Guid.NewGuid():N}"[..Math.Min(80, prefix.Length + 33)];

    private static object MinimalRequest(string title, IEnumerable<string>? langs = null) => new
    {
        Title = title,
        InputMode = "Text",
        SourceLanguageCode = "en",
        SourceText = "This is the source content for the learning.",
        TargetLanguageCodes = langs?.ToArray() ?? new[] { "fr" },
        AudienceRole = "Operator",
        PreserveSourceWording = false,
        IncludeQuiz = true,
    };

    private async Task<InitialisedTalkDto> InitialiseAsync(object request)
    {
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/initialise", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InitialisedTalkDto>()
               ?? throw new InvalidOperationException("Initialise returned null");
    }

    private async Task<ToolboxTalk?> GetTalkFromDbAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<ToolboxTalk>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    // 1 — Happy path (Text mode, all optional audit fields) → 201, Draft shell created
    [Fact]
    public async Task TextMode_AllFields_Creates201DraftTalk()
    {
        var title = UniqueTitle("Manual Handling Safety");
        var request = new
        {
            Title = title,
            InputMode = "Text",
            SourceLanguageCode = "en",
            SourceText = "Always bend your knees when lifting heavy loads.",
            TargetLanguageCodes = new[] { "fr", "de" },
            AudienceRole = "Operator",
            PreserveSourceWording = true,
            IncludeQuiz = true,
            ReviewerName = "Alice Tester",
            ReviewerOrg = "Test Org",
            ReviewerRole = "Safety Manager",
            ClientName = "Acme Corp",
            AuditPurpose = "Regulatory compliance",
        };

        var result = await InitialiseAsync(request);

        result.Id.Should().NotBeEmpty();
        result.Title.Should().Be(title);
        result.Status.Should().Be(ToolboxTalkStatus.Draft);
        result.IsActive.Should().BeTrue(); // reflects test-tenant DefaultIsActive = true (entity default)
        result.LastEditedStep.Should().Be(1);
        result.ReviewerName.Should().Be("Alice Tester");
        result.ReviewerOrg.Should().Be("Test Org");
        result.ReviewerRole.Should().Be("Safety Manager");
        result.ClientName.Should().Be("Acme Corp");
        result.AuditPurpose.Should().Be("Regulatory compliance");
        result.PreserveSourceWording.Should().BeTrue();

        // DocumentRef should be auto-generated (DOC- prefix)
        result.DocumentRef.Should().NotBeNullOrEmpty()
              .And.StartWith("DOC-");

        // TargetLanguageCodes stored as JSON string
        result.TargetLanguageCodes.Should().NotBeNullOrEmpty();

        // Sections and questions are NOT created at Step 1
        var db = await GetTalkFromDbAsync(result.Id);
        db.Should().NotBeNull();
        db!.Sections.Should().BeNullOrEmpty();
    }

    // 2 — Happy path (Pdf mode) → 201, SourceFileUrl persisted
    [Fact]
    public async Task PdfMode_WithFileUrl_CreatesTalk()
    {
        var title = UniqueTitle("Fire Safety PDF");
        var request = new
        {
            Title = title,
            InputMode = "Pdf",
            SourceLanguageCode = "en",
            SourceFileUrl = "https://pub-xxx.r2.dev/uploads/test/fire-safety.pdf",
            SourceFileName = "fire-safety.pdf",
            SourceFileType = "application/pdf",
            TargetLanguageCodes = new[] { "es" },
            AudienceRole = "Operator",
            PreserveSourceWording = false,
            IncludeQuiz = false,
        };

        var result = await InitialiseAsync(request);

        result.Status.Should().Be(ToolboxTalkStatus.Draft);
        result.SourceFileUrl.Should().Be("https://pub-xxx.r2.dev/uploads/test/fire-safety.pdf");
        result.SourceFileName.Should().Be("fire-safety.pdf");
        result.SourceFileType.Should().Be("application/pdf");
    }

    // 2a — Pdf mode also populates PdfUrl/PdfFileName (Fix 0 — new wizard slideshow toggle)
    [Fact]
    public async Task PdfMode_SetsPdfUrlAndPdfFileNameFromSourceFile()
    {
        var title = UniqueTitle("Fire Safety PDF Slideshow");
        var request = new
        {
            Title = title,
            InputMode = "Pdf",
            SourceLanguageCode = "en",
            SourceFileUrl = "https://pub-xxx.r2.dev/uploads/test/fire-safety.pdf",
            SourceFileName = "fire-safety.pdf",
            SourceFileType = "application/pdf",
            TargetLanguageCodes = new[] { "es" },
            AudienceRole = "Operator",
            PreserveSourceWording = false,
            IncludeQuiz = false,
        };

        var result = await InitialiseAsync(request);

        var talk = await GetTalkFromDbAsync(result.Id);
        talk.Should().NotBeNull();
        talk!.PdfUrl.Should().Be("https://pub-xxx.r2.dev/uploads/test/fire-safety.pdf");
        talk.PdfFileName.Should().Be("fire-safety.pdf");
    }

    // 2b — Video mode does NOT populate PdfUrl
    [Fact]
    public async Task VideoMode_DoesNotSetPdfUrl()
    {
        var title = UniqueTitle("Chemical Handling Video PdfUrl Check");
        var request = new
        {
            Title = title,
            InputMode = "Video",
            SourceLanguageCode = "en",
            VideoUrl = "https://example.com/video.mp4",
            VideoSource = "DirectUrl",
            TargetLanguageCodes = new[] { "fr" },
            AudienceRole = "Supervisor",
            PreserveSourceWording = false,
            IncludeQuiz = true,
        };

        var result = await InitialiseAsync(request);

        var talk = await GetTalkFromDbAsync(result.Id);
        talk.Should().NotBeNull();
        talk!.PdfUrl.Should().BeNull();
        talk.PdfFileName.Should().BeNull();
    }

    // 2c — Docx mode does NOT populate PdfUrl
    [Fact]
    public async Task DocxMode_DoesNotSetPdfUrl()
    {
        var title = UniqueTitle("Docx PdfUrl Check");
        var request = new
        {
            Title = title,
            InputMode = "Docx",
            SourceLanguageCode = "en",
            SourceFileUrl = "https://pub-xxx.r2.dev/uploads/test/procedure.docx",
            SourceFileName = "procedure.docx",
            SourceFileType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            TargetLanguageCodes = new[] { "fr" },
            AudienceRole = "Operator",
            PreserveSourceWording = false,
            IncludeQuiz = false,
        };

        var result = await InitialiseAsync(request);

        var talk = await GetTalkFromDbAsync(result.Id);
        talk.Should().NotBeNull();
        talk!.PdfUrl.Should().BeNull();
        talk.PdfFileName.Should().BeNull();
    }

    // 2d — Text mode does NOT populate PdfUrl
    [Fact]
    public async Task TextMode_DoesNotSetPdfUrl()
    {
        var result = await InitialiseAsync(MinimalRequest(UniqueTitle("Text Mode PdfUrl Check")));

        var talk = await GetTalkFromDbAsync(result.Id);
        talk.Should().NotBeNull();
        talk!.PdfUrl.Should().BeNull();
        talk.PdfFileName.Should().BeNull();
    }

    // 3 — Happy path (Video mode with URL) → 201
    [Fact]
    public async Task VideoMode_WithUrl_CreatesTalk()
    {
        var title = UniqueTitle("Chemical Handling Video");
        var request = new
        {
            Title = title,
            InputMode = "Video",
            SourceLanguageCode = "en",
            VideoUrl = "https://example.com/video.mp4",
            VideoSource = "DirectUrl",
            TargetLanguageCodes = new[] { "fr" },
            AudienceRole = "Supervisor",
            PreserveSourceWording = false,
            IncludeQuiz = true,
        };

        var result = await InitialiseAsync(request);

        result.Status.Should().Be(ToolboxTalkStatus.Draft);
        result.Id.Should().NotBeEmpty();
    }

    // 4 — DocumentRef auto-generated when caller omits it
    [Fact]
    public async Task DocumentRef_AutoGeneratedWhenNotSupplied()
    {
        var result = await InitialiseAsync(MinimalRequest(UniqueTitle()));

        result.DocumentRef.Should().NotBeNullOrEmpty()
              .And.MatchRegex(@"^DOC-[A-Z0-9]+$");
    }

    // 5 — DocumentRef preserved when caller supplies it
    [Fact]
    public async Task DocumentRef_PreservedWhenSupplied()
    {
        var title = UniqueTitle("Custom Ref Talk");
        var request = new
        {
            Title = title,
            InputMode = "Text",
            SourceLanguageCode = "en",
            SourceText = "Some content here.",
            TargetLanguageCodes = new[] { "fr" },
            DocumentRef = "MY-CUSTOM-REF-001",
            AudienceRole = "Operator",
            PreserveSourceWording = false,
            IncludeQuiz = true,
        };

        var result = await InitialiseAsync(request);

        result.DocumentRef.Should().Be("MY-CUSTOM-REF-001");
    }

    // 6 — Duplicate title (same tenant) → 400
    [Fact]
    public async Task DuplicateTitle_Returns400()
    {
        var title = UniqueTitle("Duplicate Title Talk");
        await InitialiseAsync(MinimalRequest(title)); // first — succeeds

        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/initialise",
            MinimalRequest(title)); // second — should fail

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // 7 — Empty TargetLanguageCodes (English-only) → 201
    // Empty target languages is now valid — allows English-only learnings that skip Translate/Validate steps.
    [Fact]
    public async Task EmptyTargetLanguageCodes_Returns201()
    {
        var request = MinimalRequest(UniqueTitle(), langs: Array.Empty<string>());
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/initialise", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // 8 — Missing SourceText in Text mode (no file URL) → 400
    [Fact]
    public async Task TextMode_NoSourceContent_Returns400()
    {
        var request = new
        {
            Title = UniqueTitle("No Content Talk"),
            InputMode = "Text",
            SourceLanguageCode = "en",
            SourceText = "", // empty — should fail
            TargetLanguageCodes = new[] { "fr" },
            AudienceRole = "Operator",
            PreserveSourceWording = false,
            IncludeQuiz = true,
        };

        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/initialise", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // 9 — Missing SourceFileUrl in Pdf mode → 400
    [Fact]
    public async Task PdfMode_NoFileUrl_Returns400()
    {
        var request = new
        {
            Title = UniqueTitle("PDF No File"),
            InputMode = "Pdf",
            SourceLanguageCode = "en",
            // SourceFileUrl omitted intentionally
            TargetLanguageCodes = new[] { "fr" },
            AudienceRole = "Operator",
            PreserveSourceWording = false,
            IncludeQuiz = true,
        };

        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/initialise", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // 10 — Invalid AudienceRole → 400
    [Fact]
    public async Task InvalidAudienceRole_Returns400()
    {
        var request = new
        {
            Title = UniqueTitle("Bad Role Talk"),
            InputMode = "Text",
            SourceLanguageCode = "en",
            SourceText = "Some content.",
            TargetLanguageCodes = new[] { "fr" },
            AudienceRole = "InvalidRole", // not Operator/Supervisor/Auditor
            PreserveSourceWording = false,
            IncludeQuiz = true,
        };

        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/initialise", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // 11 — Unauthenticated → 401
    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var response = await UnauthenticatedClient.PostAsJsonAsync(
            "/api/toolbox-talks/initialise",
            MinimalRequest(UniqueTitle()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 12 — Operator (no Learnings.Manage) → 403
    [Fact]
    public async Task Operator_Returns403()
    {
        var response = await OperatorClient.PostAsJsonAsync(
            "/api/toolbox-talks/initialise",
            MinimalRequest(UniqueTitle()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── local DTOs ────────────────────────────────────────────────────────────

    private record InitialisedTalkDto(
        Guid Id,
        string Title,
        [property: JsonConverter(typeof(JsonStringEnumConverter))]
        ToolboxTalkStatus Status,
        bool IsActive,
        int? LastEditedStep,
        string? SourceFileUrl,
        string? SourceFileName,
        string? SourceFileType,
        string? TargetLanguageCodes,
        string? ReviewerName,
        string? ReviewerOrg,
        string? ReviewerRole,
        string? DocumentRef,
        string? ClientName,
        string? AuditPurpose,
        bool PreserveSourceWording);
}
