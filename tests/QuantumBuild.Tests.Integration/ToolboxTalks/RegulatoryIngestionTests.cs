using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QuantumBuild.Core.Application.Configuration;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Pdf;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Covers the regulatory ingestion URI-validation, exception-handling, and failure-state
/// chunk (docs/regulatory-ingestion-recon.md). Two layers are exercised:
///
/// 1. Controller-level URI validation (POST /api/regulatory/documents/{id}/ingest) — a
///    Windows path or malformed URL must be rejected with 400 before any job is enqueued.
/// 2. Job-level failure/success state — RequirementIngestionJob.ExecuteAsync is invoked
///    directly (constructed manually with fakes for IPdfExtractionService and the Claude
///    HttpClient) so each fetch/parse outcome can be asserted deterministically without any
///    real network or Anthropic API call.
/// </summary>
[Collection("Integration")]
public class RegulatoryIngestionTests : IntegrationTestBase
{
    public RegulatoryIngestionTests(CustomWebApplicationFactory factory) : base(factory) { }

    // Mirrors RegulatoryIngestionController.StartIngestionRequest's JSON shape without taking
    // a direct dependency on the Controllers namespace from the test project.
    private record IngestRequestBody(string SourceUrl);

    // ─────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────────

    private static async Task<RegulatoryDocument> CreateDocumentAsync(
        ApplicationDbContext context, string? sourceUrl = null)
    {
        var body = new RegulatoryBody
        {
            Id = Guid.NewGuid(),
            Name = "Ingestion Test Body",
            Code = $"ITB{Guid.NewGuid():N}"[..10],
            Country = "IE"
        };
        var document = new RegulatoryDocument
        {
            Id = Guid.NewGuid(),
            RegulatoryBodyId = body.Id,
            Title = "Ingestion Test Document",
            Version = "1.0",
            SourceUrl = sourceUrl
        };

        context.RegulatoryBodies.Add(body);
        context.RegulatoryDocuments.Add(document);
        await context.SaveChangesAsync();

        return document;
    }

    /// <summary>
    /// Same as CreateDocumentAsync, but with an active RegulatoryProfile attached — needed for
    /// tests where the extraction outcome itself (not the "no active profile" skip) is under test.
    /// </summary>
    private static async Task<RegulatoryDocument> CreateDocumentWithProfileAsync(
        ApplicationDbContext context, string? sourceUrl = null)
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];

        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            Key = $"ingest-test-{uniqueSuffix}",
            Name = $"Ingestion Test Sector {uniqueSuffix}",
            DisplayOrder = 99,
            IsActive = true
        };
        var body = new RegulatoryBody
        {
            Id = Guid.NewGuid(),
            Name = "Ingestion Test Body",
            Code = $"ITB{uniqueSuffix}"[..10],
            Country = "IE"
        };
        var document = new RegulatoryDocument
        {
            Id = Guid.NewGuid(),
            RegulatoryBodyId = body.Id,
            Title = "Ingestion Test Document",
            Version = "1.0",
            SourceUrl = sourceUrl
        };
        var profile = new RegulatoryProfile
        {
            Id = Guid.NewGuid(),
            RegulatoryDocumentId = document.Id,
            SectorId = sector.Id,
            SectorKey = sector.Key,
            ScoreLabel = "Test Score",
            ExportLabel = "TSP",
            Description = "Integration test profile",
            IsActive = true
        };

        context.Sectors.Add(sector);
        context.RegulatoryBodies.Add(body);
        context.RegulatoryDocuments.Add(document);
        context.RegulatoryProfiles.Add(profile);
        await context.SaveChangesAsync();

        return document;
    }

    /// <summary>
    /// Builds a RequirementIngestionJob with fully controlled dependencies — no DI resolution,
    /// no real network calls. The Claude HTTP call always goes through
    /// FakeAnthropicHttpMessageHandler; the PDF fetch step goes through whatever
    /// IPdfExtractionService is passed in.
    /// </summary>
    private RequirementIngestionJob BuildJob(
        IToolboxTalksDbContext dbContext,
        IPdfExtractionService pdfExtractionService,
        string claudeResponseText = "[]",
        string? claudeStopReason = null)
    {
        var httpClient = new HttpClient(new FakeAnthropicHttpMessageHandler
        {
            ResponseContentText = claudeResponseText,
            StopReason = claudeStopReason
        });

        var settings = Options.Create(new SubtitleProcessingSettings
        {
            Claude = new QuantumBuild.Core.Application.Abstractions.AI.ClaudeSettings
            {
                BaseUrl = "https://fake-claude.test",
                ApiKey = "test-key"
            }
        });

        var aiProviders = Options.Create(new AIProviderOptions
        {
            Anthropic = new AnthropicProviderOptions
            {
                Models = new AnthropicModels { Sonnet = "claude-sonnet-test", Haiku = "claude-haiku-test" }
            }
        });

        var aiUsageLogger = GetService<IAiUsageLogger>();

        return new RequirementIngestionJob(
            dbContext,
            pdfExtractionService,
            httpClient,
            settings,
            aiUsageLogger,
            NullLogger<RequirementIngestionJob>.Instance,
            aiProviders);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Controller-level URI validation (Part A)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartIngestion_WindowsPath_Returns400WithoutEnqueueingJob()
    {
        var context = GetDbContext();
        var document = await CreateDocumentAsync(context);

        var (response, body) = await AdminClient.PostWithResponseAsync<IngestRequestBody, object>(
            $"/api/regulatory/documents/{document.Id}/ingest",
            new IngestRequestBody(@"C:\Users\bob\document.pdf"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var freshContext = GetDbContext();
        var reloaded = await freshContext.RegulatoryDocuments
            .FirstAsync(d => d.Id == document.Id);

        // Validation runs before enqueue — the job never ran, so status stays Idle even
        // though the (invalid) SourceUrl the user typed is still persisted for correction.
        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Idle);
        reloaded.SourceUrl.Should().Be(@"C:\Users\bob\document.pdf");
    }

    [Fact]
    public async Task StartIngestion_MalformedUri_Returns400()
    {
        var context = GetDbContext();
        var document = await CreateDocumentAsync(context);

        var (response, _) = await AdminClient.PostWithResponseAsync<IngestRequestBody, object>(
            $"/api/regulatory/documents/{document.Id}/ingest",
            new IngestRequestBody("not a url"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StartIngestion_FtpScheme_Returns400()
    {
        var context = GetDbContext();
        var document = await CreateDocumentAsync(context);

        var (response, _) = await AdminClient.PostWithResponseAsync<IngestRequestBody, object>(
            $"/api/regulatory/documents/{document.Id}/ingest",
            new IngestRequestBody("ftp://example.com/document.pdf"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StartIngestion_HttpsUrl_EnqueuesJobSuccessfully()
    {
        var context = GetDbContext();
        var document = await CreateDocumentAsync(context);

        var (response, dto) = await AdminClient.PostWithResponseAsync<IngestRequestBody, IngestionSessionDto>(
            $"/api/regulatory/documents/{document.Id}/ingest",
            new IngestRequestBody("https://example.com/document.pdf"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto.Should().NotBeNull();
        dto!.SourceUrl.Should().Be("https://example.com/document.pdf");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Job-level failure / success state (Parts B + C)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_UnreachableUrl_SetsStatusFailedWithFetchFailed()
    {
        var context = GetDbContext();
        var document = await CreateDocumentAsync(context, "https://unreachable.example.test/document.pdf");

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = true,
            NextErrorCategory = PdfExtractionErrorCategory.NetworkError,
            NextErrorMessage = "Failed to download PDF: connection refused"
        };

        var job = BuildJob(context, fakePdf);
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Failed);
        reloaded.LastIngestionErrorCode.Should().Be("fetch_failed");
        reloaded.LastIngestionErrorMessage.Should().NotBeNullOrWhiteSpace();
        reloaded.LastIngestedAt.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_UnparseableContent_SetsStatusFailedWithParseFailed()
    {
        var context = GetDbContext();
        var document = await CreateDocumentAsync(context, "https://example.test/document.pdf");

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = true,
            NextErrorCategory = PdfExtractionErrorCategory.ParseFailure,
            NextErrorMessage = "Invalid PDF format. The file may be corrupted."
        };

        var job = BuildJob(context, fakePdf);
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Failed);
        reloaded.LastIngestionErrorCode.Should().Be("parse_failed");
        reloaded.LastIngestedAt.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_NoActiveProfile_SetsStatusSkippedWithNoActiveProfiles()
    {
        // Document has no RegulatoryProfile at all (per docs/regulatory-extraction-size-recon.md
        // §6) — even though extraction itself completed cleanly, there is nowhere to persist to.
        // This must read as Skipped, not Success, so it is distinguishable from a real ingestion.
        var context = GetDbContext();
        var document = await CreateDocumentAsync(context, "https://example.test/document.pdf");

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = false,
            NextExtractedText = "This regulation requires staff to complete manual handling training annually."
        };

        var job = BuildJob(context, fakePdf, claudeResponseText: "[]");
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Skipped);
        reloaded.LastIngestionErrorCode.Should().Be("no_active_profiles");
        reloaded.LastIngestionErrorMessage.Should().NotBeNullOrWhiteSpace();
        reloaded.LastIngestedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ActiveProfileButEmptyExtraction_SetsStatusFailedWithZeroRequirements()
    {
        // An active profile exists, so there is somewhere to persist to — a well-formed but
        // empty Claude response is now a loud failure, not a silent Success (recon §4 item 3).
        var context = GetDbContext();
        var document = await CreateDocumentWithProfileAsync(context, "https://example.test/document.pdf");

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = false,
            NextExtractedText = "This regulation requires staff to complete manual handling training annually."
        };

        var job = BuildJob(context, fakePdf, claudeResponseText: "[]");
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Failed);
        reloaded.LastIngestionErrorCode.Should().Be("extraction_zero_requirements");
        reloaded.LastIngestionErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_ClaudeResponseTruncated_SetsStatusFailedWithExtractionTruncated()
    {
        // stop_reason=max_tokens on both the initial attempt and the retry must be treated as a
        // failure outright — not a parseable-or-not question (recon §2/§3).
        var context = GetDbContext();
        var document = await CreateDocumentWithProfileAsync(context, "https://example.test/document.pdf");

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = false,
            NextExtractedText = "This regulation requires staff to complete manual handling training annually."
        };

        var job = BuildJob(
            context,
            fakePdf,
            claudeResponseText: "[{\"title\": \"Incomplete requirement, cut off mid-fi",
            claudeStopReason: "max_tokens");
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Failed);
        reloaded.LastIngestionErrorCode.Should().Be("extraction_truncated");
        reloaded.LastIngestionErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_ClaudeResponseInvalidJson_SetsStatusFailedWithExtractionInvalidJson()
    {
        // Unparseable garbage with no truncation signal (stop_reason=end_turn) must be
        // distinguishable from the max_tokens truncation case.
        var context = GetDbContext();
        var document = await CreateDocumentWithProfileAsync(context, "https://example.test/document.pdf");

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = false,
            NextExtractedText = "This regulation requires staff to complete manual handling training annually."
        };

        var job = BuildJob(
            context,
            fakePdf,
            claudeResponseText: "I'm sorry, I cannot process this document.",
            claudeStopReason: "end_turn");
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Failed);
        reloaded.LastIngestionErrorCode.Should().Be("extraction_invalid_json");
        reloaded.LastIngestionErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_ActiveProfileWithRequirements_SetsStatusSuccessAndPersistsDrafts()
    {
        var context = GetDbContext();
        var document = await CreateDocumentWithProfileAsync(context, "https://example.test/document.pdf");

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = false,
            NextExtractedText = "This regulation requires staff to complete manual handling training annually."
        };

        const string claudeJson = """
        [
            {
                "title": "Manual Handling Training",
                "description": "Staff must complete manual handling training annually.",
                "section": "Standard 2.3",
                "sectionLabel": "Staff Training",
                "principle": "P2",
                "principleLabel": "Safety & Wellbeing",
                "priority": "high",
                "displayOrder": 1
            }
        ]
        """;

        var job = BuildJob(context, fakePdf, claudeResponseText: claudeJson, claudeStopReason: "end_turn");
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Success);
        reloaded.LastIngestedAt.Should().NotBeNull();
        reloaded.LastIngestionErrorCode.Should().BeNull();
        reloaded.LastIngestionErrorMessage.Should().BeNull();

        var profile = await context.RegulatoryProfiles.FirstAsync(p => p.RegulatoryDocumentId == document.Id);
        var persisted = await context.RegulatoryRequirements
            .Where(r => r.RegulatoryProfileId == profile.Id)
            .ToListAsync();
        persisted.Should().ContainSingle(r => r.Title == "Manual Handling Training");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidUriAlreadyPersisted_SetsStatusFailedWithInvalidUri()
    {
        // Defensive re-check inside the job itself: a document whose SourceUrl was written
        // before this validation existed (or written directly to the DB) must still fail
        // safely rather than reaching HttpClient with a "file://" URI.
        var context = GetDbContext();
        var document = await CreateDocumentAsync(context, @"C:\Users\bob\document.pdf");

        var fakePdf = new FakePdfExtractionService();
        var job = BuildJob(context, fakePdf);
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Failed);
        reloaded.LastIngestionErrorCode.Should().Be("invalid_uri");
    }
}
