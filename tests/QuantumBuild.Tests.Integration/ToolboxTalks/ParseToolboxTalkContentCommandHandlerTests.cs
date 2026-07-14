using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using System.Net;
using System.Text.Json.Serialization;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for POST /api/toolbox-talks/{id}/parse (Phase 5.3b Step 2).
/// Tests ParseToolboxTalkContentCommandHandler — materialises sections from text, PDF, or video.
/// Also covers PUT /api/toolbox-talks/{id}/sections (UpdateToolboxTalkSectionsCommandHandler).
///
/// Dispatch: AdminClient (Learnings.Manage) for success paths.
///           OperatorClient for 403 checks.
///           UnauthenticatedClient for 401 checks.
///
/// Fakes: FakeContentParserService (no AI call) + FakePdfExtractionService (no HTTP).
///        Reset NextSections per-test when default two-section output won't do.
/// </summary>
[Collection("Integration")]
public class ParseToolboxTalkContentCommandHandlerTests : IntegrationTestBase
{
    public ParseToolboxTalkContentCommandHandlerTests(CustomWebApplicationFactory factory)
        : base(factory) { }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string UniqueTitle(string prefix = "Parse Test") =>
        $"{prefix} {Guid.NewGuid():N}"[..Math.Min(80, prefix.Length + 33)];

    /// <summary>Initialise a Draft ToolboxTalk via Step 1 and return its id.</summary>
    private async Task<InitTalkDto> InitialiseAsync(
        string title,
        string inputMode = "Text",
        string? sourceText = "Content for parsing.",
        string? sourceFileUrl = null,
        string? sourceFileType = null,
        string? videoUrl = null,
        string? videoSource = null)
    {
        var request = new
        {
            Title = title,
            InputMode = inputMode,
            SourceLanguageCode = "en",
            SourceText = sourceText,
            SourceFileUrl = sourceFileUrl,
            SourceFileName = sourceFileUrl != null ? "source.pdf" : (string?)null,
            SourceFileType = sourceFileType,
            VideoUrl = videoUrl,
            VideoSource = videoSource,
            TargetLanguageCodes = new[] { "fr" },
            AudienceRole = "Operator",
            PreserveSourceWording = false,
            IncludeQuiz = true,
        };
        // Serialize with WhenWritingNull so VideoSource is omitted when null.
        // VideoSource is a non-nullable enum on InitialiseToolboxTalkCommand; sending
        // "VideoSource": null causes model-binding failure on non-video tests.
        var json = System.Text.Json.JsonSerializer.Serialize(request,
            new System.Text.Json.JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            });
        var response = await AdminClient.PostAsync(
            "/api/toolbox-talks/initialise",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InitTalkDto>()
               ?? throw new InvalidOperationException("Initialise returned null");
    }

    /// <summary>Force-set status in the DB to bypass business logic guards.</summary>
    private async Task SetStatusInDbAsync(Guid talkId, ToolboxTalkStatus status)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var talk = await db.Set<ToolboxTalk>()
            .IgnoreQueryFilters()
            .FirstAsync(t => t.Id == talkId);
        talk.Status = status;
        await db.SaveChangesAsync();
    }

    /// <summary>Read non-deleted sections from the DB for assertion.</summary>
    private async Task<List<DbSectionRow>> GetDbSectionsAsync(Guid talkId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<ToolboxTalkSection>()
            .IgnoreQueryFilters()
            .Where(s => s.ToolboxTalkId == talkId && !s.IsDeleted)
            .OrderBy(s => s.SectionNumber)
            .Select(s => new DbSectionRow(s.Id, s.Title, s.SectionNumber))
            .ToListAsync();
    }

    // ── parse tests ───────────────────────────────────────────────────────────

    // 1 — Text mode happy path → 200, 2 sections in response and persisted to DB
    [Fact]
    public async Task TextMode_ValidContent_Returns200WithSections()
    {
        Factory.FakeContentParserService.NextSections =
        [
            new("Section 1: Introduction", "<p>Introduction content.</p>", 1),
            new("Section 2: Key Points", "<p>Key points content.</p>", 2),
        ];
        var init = await InitialiseAsync(UniqueTitle(), "Text",
            sourceText: "Always bend your knees when lifting heavy loads.");

        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{init.Id}/parse", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TalkResult>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(init.Id);
        result.Status.Should().Be("Draft");
        result.LastEditedStep.Should().Be(2);
        result.Sections.Should().HaveCount(2);
        result.Sections[0].Title.Should().Be("Section 1: Introduction");
        result.Sections[1].Title.Should().Be("Section 2: Key Points");

        var db = await GetDbSectionsAsync(init.Id);
        db.Should().HaveCount(2);
    }

    // 2 — PDF mode happy path → 200, sections from fake extracted text
    [Fact]
    public async Task PdfMode_ValidFileUrl_Returns200WithSections()
    {
        Factory.FakePdfExtractionService.NextExtractedText = "Extracted PDF text.";
        Factory.FakeContentParserService.NextSections =
        [
            new("Safety Procedures", "<p>Safety content.</p>", 1),
            new("Emergency Response", "<p>Emergency content.</p>", 2),
        ];

        var init = await InitialiseAsync(UniqueTitle("PDF Parse"), "Pdf",
            sourceText: null,
            sourceFileUrl: "https://pub-xxx.r2.dev/uploads/test/safety.pdf",
            sourceFileType: "application/pdf");

        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{init.Id}/parse", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TalkResult>();
        result!.LastEditedStep.Should().Be(2);
        result.Sections.Should().HaveCount(2);
        result.Sections[0].Title.Should().Be("Safety Procedures");
        result.Sections[1].Title.Should().Be("Emergency Response");

        var db = await GetDbSectionsAsync(init.Id);
        db.Should().HaveCount(2);
    }

    // 2b — DOCX mode happy path → 200, sections from fake extracted text
    [Fact]
    public async Task DocxMode_ValidFileUrl_Returns200WithSections()
    {
        Factory.FakeDocxExtractionService.NextExtractedText =
            "Extracted Word document text for testing purposes. This text is definitely long enough.";
        Factory.FakeContentParserService.NextSections =
        [
            new("Manual Handling Procedure", "<p>Procedure content.</p>", 1),
            new("Risk Assessment", "<p>Risk content.</p>", 2),
        ];

        var init = await InitialiseAsync(UniqueTitle("DOCX Parse"), "Docx",
            sourceText: null,
            sourceFileUrl: "https://pub-xxx.r2.dev/uploads/test/safety-sop.docx",
            sourceFileType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{init.Id}/parse", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TalkResult>();
        result.Should().NotBeNull();
        result!.LastEditedStep.Should().Be(2);
        result.Sections.Should().HaveCount(2);
        result.Sections[0].Title.Should().Be("Manual Handling Procedure");
        result.Sections[1].Title.Should().Be("Risk Assessment");

        var db = await GetDbSectionsAsync(init.Id);
        db.Should().HaveCount(2);
    }

    // 3 — Video mode happy path → 200, status = Processing, sections empty (job enqueued)
    [Fact]
    public async Task VideoMode_WithVideoUrl_Returns200StatusProcessing()
    {
        var init = await InitialiseAsync(UniqueTitle("Video Parse"), "Video",
            sourceText: null,
            videoUrl: "https://example.com/safety-video.mp4",
            videoSource: "DirectUrl");

        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{init.Id}/parse", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TalkResult>();
        result!.Status.Should().Be("Processing");
        result.Sections.Should().BeEmpty();
        result.LastEditedStep.Should().Be(2);

        // No sections in DB — job hasn't executed yet in test env
        var db = await GetDbSectionsAsync(init.Id);
        db.Should().BeEmpty();
    }

    // 4 — Talk not in Draft status → 409 Conflict
    [Fact]
    public async Task NonDraftTalk_Returns409Conflict()
    {
        var init = await InitialiseAsync(UniqueTitle("Published Talk"));
        await SetStatusInDbAsync(init.Id, ToolboxTalkStatus.Published);

        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{init.Id}/parse", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // 5 — Random Guid → 404
    [Fact]
    public async Task TalkNotFound_Returns404()
    {
        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{Guid.NewGuid()}/parse", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 6 — No JWT → 401
    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var init = await InitialiseAsync(UniqueTitle("Auth Test"));

        var response = await UnauthenticatedClient.PostAsync(
            $"/api/toolbox-talks/{init.Id}/parse", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // 7 — Operator (no Learnings.Manage) → 403
    [Fact]
    public async Task Operator_Returns403()
    {
        var init = await InitialiseAsync(UniqueTitle("403 Test"));

        var response = await OperatorClient.PostAsync(
            $"/api/toolbox-talks/{init.Id}/parse", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // 8 — Re-parsing the same talk replaces prior sections
    [Fact]
    public async Task Idempotency_ReparseClearsPriorSections()
    {
        var init = await InitialiseAsync(UniqueTitle("Idempotent"), "Text",
            sourceText: "Source text for idempotency test.");

        // First parse → 2 sections
        Factory.FakeContentParserService.NextSections =
        [
            new("First Pass A", "<p>Content A.</p>", 1),
            new("First Pass B", "<p>Content B.</p>", 2),
        ];
        (await AdminClient.PostAsync($"/api/toolbox-talks/{init.Id}/parse", null))
            .EnsureSuccessStatusCode();

        // Second parse → 3 different sections
        Factory.FakeContentParserService.NextSections =
        [
            new("Second Pass A", "<p>New A.</p>", 1),
            new("Second Pass B", "<p>New B.</p>", 2),
            new("Second Pass C", "<p>New C.</p>", 3),
        ];
        var secondResponse = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{init.Id}/parse", null);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await secondResponse.Content.ReadFromJsonAsync<TalkResult>();
        result!.Sections.Should().HaveCount(3);
        result.Sections[0].Title.Should().Be("Second Pass A");

        // DB: only 3 active sections (first 2 soft-deleted)
        var db = await GetDbSectionsAsync(init.Id);
        db.Should().HaveCount(3);
        db.Should().Contain(s => s.Title == "Second Pass A");
        db.Should().NotContain(s => s.Title == "First Pass A");
    }

    // ── update-sections tests ────────────────────────────────────────────────

    // 9 — PUT /sections upserts + soft-deletes correctly
    [Fact]
    public async Task UpdateSections_UpsertAndDelete_Returns200WithUpdatedSections()
    {
        // Setup: parse to get initial 2 sections
        Factory.FakeContentParserService.NextSections =
        [
            new("Original Section 1", "<p>Original 1.</p>", 1),
            new("Original Section 2", "<p>Original 2.</p>", 2),
        ];
        var init = await InitialiseAsync(UniqueTitle("Update Sections"), "Text",
            sourceText: "Source text for update sections test.");
        var parseResp = await AdminClient.PostAsync($"/api/toolbox-talks/{init.Id}/parse", null);
        parseResp.EnsureSuccessStatusCode();
        var parsed = await parseResp.Content.ReadFromJsonAsync<TalkResult>();
        var section1Id = parsed!.Sections[0].Id;

        // Act: keep section 1 (renamed), drop section 2, add a new section
        var updateRequest = new
        {
            Sections = new object[]
            {
                new { Id = section1Id, SectionNumber = 1, Title = "Renamed Section 1",
                      Content = "<p>Updated content.</p>", RequiresAcknowledgment = true, Source = "Manual" },
                new { Id = (Guid?)null, SectionNumber = 2, Title = "Brand New Section",
                      Content = "<p>Brand new content.</p>", RequiresAcknowledgment = true, Source = "Manual" },
            }
        };

        var response = await AdminClient.PutAsJsonAsync(
            $"/api/toolbox-talks/{init.Id}/sections", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TalkResult>();
        result!.Sections.Should().HaveCount(2);
        result.Sections[0].Title.Should().Be("Renamed Section 1");
        result.Sections[0].Id.Should().Be(section1Id);
        result.Sections[1].Title.Should().Be("Brand New Section");

        // DB: section 1 renamed, section 2 soft-deleted, section 3 new
        var db = await GetDbSectionsAsync(init.Id);
        db.Should().HaveCount(2);
        db.Should().Contain(s => s.Title == "Renamed Section 1");
        db.Should().Contain(s => s.Title == "Brand New Section");
    }

    // ── local DTOs ────────────────────────────────────────────────────────────

    private record InitTalkDto(Guid Id, string Status);

    private record TalkResult(
        Guid Id,
        string Status,
        int? LastEditedStep,
        List<SectionResult> Sections);

    private record SectionResult(Guid Id, string Title, int SectionNumber);

    private record DbSectionRow(Guid Id, string Title, int SectionNumber);
}
