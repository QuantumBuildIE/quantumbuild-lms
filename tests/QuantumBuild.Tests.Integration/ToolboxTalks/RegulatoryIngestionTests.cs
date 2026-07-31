using System.Text.Json;
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
/// Covers the regulatory ingestion URI-validation, exception-handling, failure-state, and
/// per-principle segmentation chunks (docs/regulatory-ingestion-recon.md,
/// docs/regulatory-extraction-rebuild-recon.md). Three layers are exercised:
///
/// 1. Controller-level URI validation (POST /api/regulatory/documents/{id}/ingest) — a
///    Windows path or malformed URL must be rejected with 400 before any job is enqueued.
/// 2. Job-level failure/success state — RequirementIngestionJob.ExecuteAsync is invoked
///    directly (constructed manually with fakes for IPdfExtractionService and the Claude
///    HttpClient) so each fetch/parse outcome can be asserted deterministically without any
///    real network or Anthropic API call.
/// 3. Segmented extraction — RequirementIngestionJob now makes one Claude call per principle
///    (RequirementIngestionJob.PrincipleNumbers = 1..4) rather than one whole-document call.
///    FakeAnthropicHttpMessageHandler.Responder lets these tests vary the canned response per
///    call, keyed off the "Principle {N}" text the segmented prompt embeds, so each principle
///    can be made to succeed, truncate, or return an incomplete set of standards independently.
/// </summary>
[Collection("Integration")]
public class RegulatoryIngestionTests : IntegrationTestBase
{
    public RegulatoryIngestionTests(CustomWebApplicationFactory factory) : base(factory) { }

    // Mirrors RegulatoryIngestionController.StartIngestionRequest's JSON shape without taking
    // a direct dependency on the Controllers namespace from the test project.
    private record IngestRequestBody(string SourceUrl);

    // Mirrors RequirementIngestionJob.HiqaExpectedStandardsByPrinciple exactly — the production
    // completeness check compares extracted Section text against this same structure, so test
    // fixtures built from it will (or won't, when deliberately made partial) satisfy that check.
    private static readonly Dictionary<int, string[]> ExpectedStandardsByPrinciple = new()
    {
        [1] = new[] { "1.1", "1.2", "1.3", "1.4" },
        [2] = new[] { "2.1", "2.2", "2.3", "2.4", "2.5" },
        [3] = new[] { "3.1", "3.2", "3.3" },
        [4] = new[] { "4.1", "4.2", "4.3", "4.4", "4.5" },
    };

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
    /// IPdfExtractionService is passed in. Convenience wrapper over BuildJobWithHandler for
    /// tests that don't need to inspect the handler afterwards.
    /// </summary>
    private RequirementIngestionJob BuildJob(
        IToolboxTalksDbContext dbContext,
        IPdfExtractionService pdfExtractionService,
        string claudeResponseText = "[]",
        string? claudeStopReason = null)
    {
        return BuildJobWithHandler(dbContext, pdfExtractionService, claudeResponseText: claudeResponseText, claudeStopReason: claudeStopReason).Job;
    }

    /// <summary>
    /// Same as BuildJob, but also returns the FakeAnthropicHttpMessageHandler so tests can
    /// assert on call count / per-call request bodies (needed for the segmented-extraction
    /// tests — e.g. confirming fail-fast stops calling remaining principles, or that a retry
    /// happened before success).
    /// </summary>
    private (RequirementIngestionJob Job, FakeAnthropicHttpMessageHandler Handler) BuildJobWithHandler(
        IToolboxTalksDbContext dbContext,
        IPdfExtractionService pdfExtractionService,
        Func<string, (string Text, string? StopReason)>? responder = null,
        string claudeResponseText = "[]",
        string? claudeStopReason = null)
    {
        var handler = new FakeAnthropicHttpMessageHandler
        {
            ResponseContentText = claudeResponseText,
            StopReason = claudeStopReason,
            Responder = responder
        };
        var httpClient = new HttpClient(handler);

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

        var job = new RequirementIngestionJob(
            dbContext,
            pdfExtractionService,
            httpClient,
            settings,
            aiUsageLogger,
            NullLogger<RequirementIngestionJob>.Instance,
            aiProviders);

        return (job, handler);
    }

    /// <summary>
    /// Extracts which principle a segmented prompt targets by searching for the literal
    /// "Principle {N}" text RequirementIngestionJob.BuildExtractionPrompt embeds — the same
    /// signal a human reviewing a captured request body would use.
    /// </summary>
    private static int ExtractPrincipleNumber(string requestBody)
    {
        for (var n = 1; n <= 4; n++)
        {
            if (requestBody.Contains($"Principle {n}", StringComparison.Ordinal))
                return n;
        }

        throw new InvalidOperationException($"Could not determine principle number from request body: {requestBody}");
    }

    /// <summary>
    /// True when the request body is a retry (RequirementIngestionJob.BuildStricterPrompt always
    /// appends an "IMPORTANT: Your previous response..." instruction, whether the retry reason
    /// was truncation, invalid JSON, or an incomplete standard set).
    /// </summary>
    private static bool IsRetryRequest(string requestBody) =>
        requestBody.Contains("IMPORTANT: Your previous response", StringComparison.Ordinal);

    private static string BuildSegmentJson(int principleNumber, IEnumerable<string> standardIds)
    {
        var items = standardIds.Select((id, idx) => new
        {
            title = $"Principle {principleNumber} Standard {id} Requirement",
            description = $"Training requirement for standard {id}.",
            section = $"Standard {id}",
            sectionLabel = "Segment Test",
            principle = $"P{principleNumber}",
            principleLabel = $"Test Principle {principleNumber}",
            priority = "med",
            displayOrder = idx + 1
        });
        return JsonSerializer.Serialize(items);
    }

    /// <summary>
    /// Builds a Responder that returns a fully complete, non-truncated segment for whichever
    /// principle the request targets — the default "everything succeeds" shape most tests need
    /// as their baseline, with an <paramref name="overrideFor"/> hook to make one principle
    /// behave abnormally (truncate, return a partial standard set, etc.) while every other
    /// principle still succeeds normally.
    /// </summary>
    private static Func<string, (string Text, string? StopReason)> BuildSegmentedResponder(
        Func<int, bool, (string Text, string? StopReason)?>? overrideFor = null)
    {
        return requestBody =>
        {
            var principle = ExtractPrincipleNumber(requestBody);
            var isRetry = IsRetryRequest(requestBody);

            var overridden = overrideFor?.Invoke(principle, isRetry);
            if (overridden != null)
                return overridden.Value;

            return (BuildSegmentJson(principle, ExpectedStandardsByPrinciple[principle]), "end_turn");
        };
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
        // §6) — even though extraction itself completed cleanly across all 4 principle segments,
        // there is nowhere to persist to. This must read as Skipped, not Success, so it is
        // distinguishable from a real ingestion. Uses the full-success responder (not a trivial
        // "[]") so the segmentation completeness check (§3 below) doesn't fail the run before
        // this branch is even reached.
        var context = GetDbContext();
        var document = await CreateDocumentAsync(context, "https://example.test/document.pdf");

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = false,
            NextExtractedText = "This regulation requires staff to complete manual handling training annually."
        };

        var (job, handler) = BuildJobWithHandler(context, fakePdf, responder: BuildSegmentedResponder());
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Skipped);
        reloaded.LastIngestionErrorCode.Should().Be("no_active_profiles");
        reloaded.LastIngestionErrorMessage.Should().NotBeNullOrWhiteSpace();
        reloaded.LastIngestedAt.Should().NotBeNull();
        handler.RequestBodies.Should().HaveCount(4, "one call per principle, no retries needed");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Segmented extraction (docs/regulatory-extraction-rebuild-recon.md)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_AllPrinciplesSucceed_SetsStatusSuccessAndPersistsCoherentDisplayOrder()
    {
        // Full 4-principle success: each principle's segment returns exactly its expected
        // standards (4 + 5 + 3 + 5 = 17 total). Confirms both the happy path and — the point of
        // item 4 in the rebuild chunk — that DisplayOrder is assigned coherently across all 4
        // segments at assembly (no restarts-at-1 collisions between principles).
        var context = GetDbContext();
        var document = await CreateDocumentWithProfileAsync(context, "https://example.test/document.pdf");

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = false,
            NextExtractedText = "This regulation requires staff to complete manual handling training annually."
        };

        var (job, handler) = BuildJobWithHandler(context, fakePdf, responder: BuildSegmentedResponder());
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);
        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Success);
        reloaded.LastIngestedAt.Should().NotBeNull();
        reloaded.LastIngestionErrorCode.Should().BeNull();
        reloaded.LastIngestionErrorMessage.Should().BeNull();

        handler.RequestBodies.Should().HaveCount(4, "one call per principle, no retries needed");

        var profile = await context.RegulatoryProfiles.FirstAsync(p => p.RegulatoryDocumentId == document.Id);
        var persisted = await context.RegulatoryRequirements
            .Where(r => r.RegulatoryProfileId == profile.Id)
            .OrderBy(r => r.DisplayOrder)
            .ToListAsync();

        var expectedTotal = ExpectedStandardsByPrinciple.Values.Sum(s => s.Length);
        persisted.Should().HaveCount(expectedTotal);

        // Coherent, non-colliding: exactly 1..N once each, no restarts per segment.
        persisted.Select(r => r.DisplayOrder).Should().BeEquivalentTo(
            Enumerable.Range(1, expectedTotal), options => options.WithoutStrictOrdering());
        persisted.Select(r => r.DisplayOrder).Should().OnlyHaveUniqueItems();

        // Assembled in principle order (1, 2, 3, 4) with each segment's own order preserved —
        // DisplayOrder 1 is Principle 1's first standard, DisplayOrder 5 is Principle 2's first.
        persisted[0].Section.Should().Be("Standard 1.1");
        persisted[3].Section.Should().Be("Standard 1.4");
        persisted[4].Section.Should().Be("Standard 2.1");
        persisted[^1].Section.Should().Be("Standard 4.5");
    }

    [Fact]
    public async Task ExecuteAsync_TruncatedSegmentRetriesAndSucceeds_SetsStatusSuccess()
    {
        // Principle 2's first attempt truncates (stop_reason=max_tokens); the retry succeeds.
        // Every other principle succeeds on its first attempt. Confirms the per-segment retry
        // (item 2 of the rebuild chunk) is scoped to the failing segment only.
        var context = GetDbContext();
        var document = await CreateDocumentWithProfileAsync(context, "https://example.test/document.pdf");

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = false,
            NextExtractedText = "This regulation requires staff to complete manual handling training annually."
        };

        var responder = BuildSegmentedResponder((principle, isRetry) =>
            principle == 2 && !isRetry
                ? ("[{\"title\": \"Incomplete requirement, cut off mid-fi", "max_tokens")
                : null);

        var (job, handler) = BuildJobWithHandler(context, fakePdf, responder: responder);
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);
        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Success);

        // 1 (P1) + 2 (P2 attempt + retry) + 1 (P3) + 1 (P4) = 5 total Claude calls.
        handler.RequestBodies.Should().HaveCount(5);

        var profile = await context.RegulatoryProfiles.FirstAsync(p => p.RegulatoryDocumentId == document.Id);
        var persistedCount = await context.RegulatoryRequirements
            .CountAsync(r => r.RegulatoryProfileId == profile.Id);
        persistedCount.Should().Be(ExpectedStandardsByPrinciple.Values.Sum(s => s.Length));
    }

    [Fact]
    public async Task ExecuteAsync_ClaudeResponseTruncated_SetsStatusFailedWithExtractionTruncated()
    {
        // stop_reason=max_tokens on both the initial attempt and the retry must be treated as a
        // failure outright — not a parseable-or-not question (recon §2/§3). Under
        // run-all-collect-failures, Principle 1 (first in PrincipleNumbers) fails both attempts,
        // but Principles 2-4 are still attempted and succeed individually — nothing is persisted
        // regardless (all-or-nothing rule, item 5 of the rebuild chunk), but every principle got a
        // chance to run rather than the loop stopping at the first failure.
        var context = GetDbContext();
        var document = await CreateDocumentWithProfileAsync(context, "https://example.test/document.pdf");

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = false,
            NextExtractedText = "This regulation requires staff to complete manual handling training annually."
        };

        var responder = BuildSegmentedResponder((principle, _) =>
            principle == 1
                ? ("[{\"title\": \"Incomplete requirement, cut off mid-fi", "max_tokens")
                : null);

        var (job, handler) = BuildJobWithHandler(context, fakePdf, responder: responder);
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Failed);
        reloaded.LastIngestionErrorCode.Should().Be("extraction_truncated");
        reloaded.LastIngestionErrorMessage.Should().NotBeNullOrWhiteSpace();

        // 2 (P1 attempt + retry, both truncated) + 1 (P2) + 1 (P3) + 1 (P4) = 5 total Claude calls.
        // All 4 principles are attempted even though Principle 1 already failed.
        handler.RequestBodies.Should().HaveCount(5, "run-all-collect-failures attempts every principle, not just the first failure");

        var profile = await context.RegulatoryProfiles.FirstAsync(p => p.RegulatoryDocumentId == document.Id);
        var persistedCount = await context.RegulatoryRequirements
            .CountAsync(r => r.RegulatoryProfileId == profile.Id);
        persistedCount.Should().Be(0, "all-or-nothing: nothing may persist when any segment fails");
    }

    [Fact]
    public async Task ExecuteAsync_SegmentMissingExpectedStandards_SetsStatusFailedWithExtractionIncomplete()
    {
        // Principle 1's segment returns valid, parseable, non-truncated JSON — but it's short:
        // only 2 of the 4 expected standards (1.1, 1.2), missing 1.3 and 1.4. This is the new
        // failure mode segmentation introduces (item 3 of the rebuild chunk) — a segment can look
        // fine and still have silently dropped content. Both the initial attempt and the retry
        // return the same partial set, so the segment fails after retry; the whole document fails
        // and nothing is persisted, same guarantee as a truncated/invalid-JSON segment. Under
        // run-all-collect-failures, Principles 2-4 are still attempted (and succeed) even though
        // Principle 1 already failed.
        var context = GetDbContext();
        var document = await CreateDocumentWithProfileAsync(context, "https://example.test/document.pdf");

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = false,
            NextExtractedText = "This regulation requires staff to complete manual handling training annually."
        };

        var responder = BuildSegmentedResponder((principle, _) =>
            principle == 1
                ? (BuildSegmentJson(1, new[] { "1.1", "1.2" }), "end_turn")
                : null);

        var (job, handler) = BuildJobWithHandler(context, fakePdf, responder: responder);
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Failed);
        reloaded.LastIngestionErrorCode.Should().Be("extraction_incomplete");
        reloaded.LastIngestionErrorMessage.Should().NotBeNullOrWhiteSpace();
        reloaded.LastIngestionErrorMessage.Should().Contain("1.3").And.Contain("1.4");

        // 2 (P1 attempt + retry, both missing standards) + 1 (P2) + 1 (P3) + 1 (P4) = 5 total calls.
        // All 4 principles are attempted even though Principle 1 already failed.
        handler.RequestBodies.Should().HaveCount(5, "run-all-collect-failures attempts every principle, not just the first failure");

        var profile = await context.RegulatoryProfiles.FirstAsync(p => p.RegulatoryDocumentId == document.Id);
        var persistedCount = await context.RegulatoryRequirements
            .CountAsync(r => r.RegulatoryProfileId == profile.Id);
        persistedCount.Should().Be(0, "all-or-nothing: nothing may persist when any segment fails its completeness check");
    }

    [Fact]
    public async Task ExecuteAsync_TwoPrinciplesFailDifferentReasons_AggregatesBothInFailureMessage()
    {
        // Principle 2 truncates on both attempts; Principle 4 is missing an expected standard on
        // both attempts. Two different failure reasons for two different principles — the
        // aggregated failure must name both, not just the first one encountered, and must still
        // attempt every principle (Principles 1 and 3 succeed normally).
        var context = GetDbContext();
        var document = await CreateDocumentWithProfileAsync(context, "https://example.test/document.pdf");

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = false,
            NextExtractedText = "This regulation requires staff to complete manual handling training annually."
        };

        var responder = BuildSegmentedResponder((principle, _) => principle switch
        {
            2 => ("[{\"title\": \"Incomplete requirement, cut off mid-fi", "max_tokens"),
            4 => (BuildSegmentJson(4, new[] { "4.1", "4.2", "4.3", "4.4" }), "end_turn"), // missing 4.5
            _ => null
        });

        var (job, handler) = BuildJobWithHandler(context, fakePdf, responder: responder);
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Failed);
        reloaded.LastIngestionErrorMessage.Should().NotBeNullOrWhiteSpace();
        reloaded.LastIngestionErrorMessage.Should().Contain("Principle 2").And.Contain("Principle 4");
        reloaded.LastIngestionErrorMessage.Should().Contain("max_tokens");
        reloaded.LastIngestionErrorMessage.Should().Contain("4.5");

        // 1 (P1) + 2 (P2 attempt + retry) + 1 (P3) + 2 (P4 attempt + retry) = 6 total Claude calls.
        // Every principle is attempted even though two of them fail for different reasons.
        handler.RequestBodies.Should().HaveCount(6, "run-all-collect-failures attempts every principle regardless of earlier failures");

        var profile = await context.RegulatoryProfiles.FirstAsync(p => p.RegulatoryDocumentId == document.Id);
        var persistedCount = await context.RegulatoryRequirements
            .CountAsync(r => r.RegulatoryProfileId == profile.Id);
        persistedCount.Should().Be(0, "all-or-nothing: nothing may persist when any segment fails");
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
    public async Task ExecuteAsync_UnexpectedClaudeApiError_SetsStatusFailedAndRethrows()
    {
        // A genuine unexpected exception (Claude API 500) is distinct from every expected,
        // non-throwing failure outcome exercised above (fetch_failed, extraction_truncated,
        // etc.) — those all return from inside the try block via their own Mark*Async call and
        // never reach ExecuteAsync's catch. This is the one path that does: CallClaudeAsync
        // throws InvalidOperationException on a non-success HTTP status, which must now (1)
        // still persist Failed on the document and (2) rethrow so Hangfire's
        // [AutomaticRetry(Attempts = 1)] can actually see the failure and fire — previously the
        // catch swallowed every exception, so Hangfire recorded the run as Succeeded regardless.
        var context = GetDbContext();
        var document = await CreateDocumentWithProfileAsync(context, "https://example.test/document.pdf");

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = false,
            NextExtractedText = "This regulation requires staff to complete manual handling training annually."
        };

        var (job, handler) = BuildJobWithHandler(context, fakePdf);
        handler.FailWithStatusCode = HttpStatusCode.InternalServerError;

        var act = async () => await job.ExecuteAsync(document.Id, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Failed);
        reloaded.LastIngestionErrorCode.Should().Be("unknown");
        reloaded.LastIngestionErrorMessage.Should().NotBeNullOrWhiteSpace();
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
