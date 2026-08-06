using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QuantumBuild.Core.Application.Configuration;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Pdf;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Regulatory;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Covers the regulatory ingestion URI-validation, exception-handling, failure-state, and
/// map-driven verbatim transcription behaviour (docs/faithful-extraction-build-recon.md). Four
/// layers are exercised:
///
/// 1. Controller-level URI validation (POST /api/regulatory/documents/{id}/ingest) — a
///    Windows path or malformed URL must be rejected with 400 before any job is enqueued.
/// 2. Job-level failure/success state — RequirementIngestionJob.ExecuteAsync is invoked
///    directly (constructed manually with fakes for IPdfExtractionService and the Claude
///    HttpClient) so each fetch/parse outcome can be asserted deterministically without any
///    real network or Anthropic API call.
/// 3. Map-driven segmented extraction — RequirementIngestionJob makes one Claude call per
///    STANDARD the document's structure map declares (not one call per hardcoded "principle" as
///    before the faithful-extraction rework). FakeAnthropicHttpMessageHandler.Responder lets
///    these tests vary the canned response per call, keyed off the "for Standard {id} (Principle"
///    text the segmented prompt embeds, so each standard can be made to succeed, truncate, or
///    return an incomplete feature set independently. Tests build their own small, fake
///    RegulatoryStructureMap fixture (CreateStructureMapAsync) rather than depending on the real
///    151-feature HIQA seed data, so the target feature set is fully known and small.
/// 4. Provisional flagging — a Draft (unverified) structure map does not block extraction, but
///    every requirement persisted from it must carry IsProvisional = true.
/// </summary>
[Collection("Integration")]
public class RegulatoryIngestionTests : IntegrationTestBase
{
    public RegulatoryIngestionTests(CustomWebApplicationFactory factory) : base(factory) { }

    // Mirrors RegulatoryIngestionController.StartIngestionRequest's JSON shape without taking
    // a direct dependency on the Controllers namespace from the test project.
    private record IngestRequestBody(string SourceUrl);

    // ─────────────────────────────────────────────────────────────────────────────
    // Fake structure map fixture — small and fully known, unlike the real 151-feature HIQA seed.
    // Three standards across two principles: enough to exercise single-segment failures,
    // multi-segment "mixed reasons" failures, and retry-then-succeed, without depending on real
    // document content.
    // ─────────────────────────────────────────────────────────────────────────────

    private static readonly (string StandardId, int Principle, string[] Person, string[] Provider)[] FakeStandards =
    {
        ("1.1", 1, new[] { "1.1.1", "1.1.2" }, new[] { "1.1.1" }),
        ("1.2", 1, new[] { "1.2.1" }, new[] { "1.2.1", "1.2.2" }),
        ("2.1", 2, new[] { "2.1.1" }, new[] { "2.1.1" }),
    };

    private static readonly IReadOnlyDictionary<string, (int Principle, string[] Person, string[] Provider)> FakeStandardsById =
        FakeStandards.ToDictionary(s => s.StandardId, s => (s.Principle, s.Person, s.Provider));

    private const string FootnotedIdentifier = "1.1.1";
    private const string FootnotedStandardId = "1.1";
    private const string FootnoteText = "Test footnote attached to person feature 1.1.1.";

    private static readonly IReadOnlyList<(string Identifier, RequirementBlock Block)> AllFakeFeatureKeys =
        FakeStandards
            .SelectMany(s => s.Person.Select(id => (id, RequirementBlock.Person))
                .Concat(s.Provider.Select(id => (id, RequirementBlock.Provider))))
            .ToList();

    // ─────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes the FakeStandards fixture as a DB-backed RegulatoryStructureMap tree for the given
    /// document — mirrors RegulatoryStructureMapSeedData's shape but with a small, fully-known
    /// feature set instead of the real 151-feature HIQA content.
    /// </summary>
    private static async Task<Guid> CreateStructureMapAsync(
        ApplicationDbContext context,
        Guid documentId,
        RegulatoryStructureMapStatus status = RegulatoryStructureMapStatus.Verified)
    {
        var now = DateTime.UtcNow;

        var map = new RegulatoryStructureMap
        {
            Id = Guid.NewGuid(),
            RegulatoryDocumentId = documentId,
            Status = status,
            VerifiedBy = status == RegulatoryStructureMapStatus.Verified ? "test-verifier" : null,
            VerifiedAt = status == RegulatoryStructureMapStatus.Verified ? DateTimeOffset.UtcNow : null,
            CreatedAt = now,
            CreatedBy = "test",
        };
        context.RegulatoryStructureMaps.Add(map);

        var principlesByNumber = new Dictionary<int, RegulatoryStructureMapPrinciple>();
        var principleDisplayOrder = 0;
        var standardDisplayOrderByPrinciple = new Dictionary<int, int>();

        foreach (var (standardId, principleNumber, personIds, providerIds) in FakeStandards)
        {
            if (!principlesByNumber.TryGetValue(principleNumber, out var principle))
            {
                principle = new RegulatoryStructureMapPrinciple
                {
                    Id = Guid.NewGuid(),
                    RegulatoryStructureMapId = map.Id,
                    Number = principleNumber,
                    DisplayOrder = principleDisplayOrder++,
                    CreatedAt = now,
                    CreatedBy = "test",
                };
                context.RegulatoryStructureMapPrinciples.Add(principle);
                principlesByNumber[principleNumber] = principle;
            }

            var standardOrder = standardDisplayOrderByPrinciple.TryGetValue(principleNumber, out var so) ? so : 0;
            standardDisplayOrderByPrinciple[principleNumber] = standardOrder + 1;

            var standard = new RegulatoryStructureMapStandard
            {
                Id = Guid.NewGuid(),
                RegulatoryStructureMapPrincipleId = principle.Id,
                StandardId = standardId,
                DisplayOrder = standardOrder,
                CreatedAt = now,
                CreatedBy = "test",
            };
            context.RegulatoryStructureMapStandards.Add(standard);

            var featureDisplayOrder = 0;
            foreach (var id in personIds)
            {
                context.RegulatoryStructureMapFeatures.Add(new RegulatoryStructureMapFeature
                {
                    Id = Guid.NewGuid(),
                    RegulatoryStructureMapStandardId = standard.Id,
                    Identifier = id,
                    Block = RequirementBlock.Person,
                    VerbatimText = $"Map ground truth for Person {id}.",
                    FootnoteDefinition = (standardId == FootnotedStandardId && id == FootnotedIdentifier) ? FootnoteText : null,
                    DisplayOrder = featureDisplayOrder++,
                    CreatedAt = now,
                    CreatedBy = "test",
                });
            }
            foreach (var id in providerIds)
            {
                context.RegulatoryStructureMapFeatures.Add(new RegulatoryStructureMapFeature
                {
                    Id = Guid.NewGuid(),
                    RegulatoryStructureMapStandardId = standard.Id,
                    Identifier = id,
                    Block = RequirementBlock.Provider,
                    VerbatimText = $"Map ground truth for Provider {id}.",
                    DisplayOrder = featureDisplayOrder++,
                    CreatedAt = now,
                    CreatedBy = "test",
                });
            }
        }

        await context.SaveChangesAsync();
        return map.Id;
    }

    private static async Task<RegulatoryDocument> CreateDocumentAsync(
        ApplicationDbContext context,
        string? sourceUrl = null,
        bool createStructureMap = false,
        RegulatoryStructureMapStatus mapStatus = RegulatoryStructureMapStatus.Verified)
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

        if (createStructureMap)
            await CreateStructureMapAsync(context, document.Id, mapStatus);

        return document;
    }

    /// <summary>
    /// Same as CreateDocumentAsync, but with an active RegulatoryProfile attached and (by default)
    /// a Verified structure map matching the FakeStandards fixture — needed for tests where the
    /// extraction outcome itself (not the "no active profile" skip or "no structure map" failure)
    /// is under test.
    /// </summary>
    private static async Task<RegulatoryDocument> CreateDocumentWithProfileAsync(
        ApplicationDbContext context,
        string? sourceUrl = null,
        bool createStructureMap = true,
        RegulatoryStructureMapStatus mapStatus = RegulatoryStructureMapStatus.Verified)
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

        if (createStructureMap)
            await CreateStructureMapAsync(context, document.Id, mapStatus);

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
    /// tests — e.g. confirming fail-fast stops calling remaining standards, or that a retry
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
        var structureMapProvider = new RegulatoryStructureMapProvider(dbContext);

        var job = new RequirementIngestionJob(
            dbContext,
            pdfExtractionService,
            structureMapProvider,
            httpClient,
            settings,
            aiUsageLogger,
            NullLogger<RequirementIngestionJob>.Instance,
            aiProviders);

        return (job, handler);
    }

    /// <summary>
    /// Extracts which standard a segmented prompt targets by searching for the literal
    /// "for Standard {id} (Principle" text RequirementIngestionJob.BuildExtractionPrompt embeds —
    /// the same signal a human reviewing a captured request body would use.
    /// </summary>
    private static string ExtractStandardId(string requestBody)
    {
        foreach (var id in FakeStandardsById.Keys)
        {
            if (requestBody.Contains($"for Standard {id} (Principle", StringComparison.Ordinal))
                return id;
        }

        throw new InvalidOperationException($"Could not determine standard id from request body: {requestBody}");
    }

    /// <summary>
    /// True when the request body is a retry (RequirementIngestionJob.BuildStricterPrompt always
    /// appends an "IMPORTANT: Your previous response..." instruction, whether the retry reason
    /// was truncation, invalid JSON, or an incomplete feature set).
    /// </summary>
    private static bool IsRetryRequest(string requestBody) =>
        requestBody.Contains("IMPORTANT: Your previous response", StringComparison.Ordinal);

    private static string BuildSegmentJson(string standardId, IEnumerable<string> personIds, IEnumerable<string> providerIds)
    {
        var items = personIds
            .Select(id => new
            {
                identifier = id,
                block = "Person",
                verbatimText = $"Verbatim text for {standardId} person {id}."
            })
            .Concat(providerIds.Select(id => new
            {
                identifier = id,
                block = "Provider",
                verbatimText = $"Verbatim text for {standardId} provider {id}."
            }));

        return JsonSerializer.Serialize(items);
    }

    /// <summary>
    /// Builds a Responder that returns a fully complete, non-truncated segment for whichever
    /// standard the request targets — the default "everything succeeds" shape most tests need
    /// as their baseline, with an <paramref name="overrideFor"/> hook to make one standard
    /// behave abnormally (truncate, return a partial feature set, etc.) while every other
    /// standard still succeeds normally.
    /// </summary>
    private static Func<string, (string Text, string? StopReason)> BuildSegmentedResponder(
        Func<string, bool, (string Text, string? StopReason)?>? overrideFor = null)
    {
        return requestBody =>
        {
            var standardId = ExtractStandardId(requestBody);
            var isRetry = IsRetryRequest(requestBody);

            var overridden = overrideFor?.Invoke(standardId, isRetry);
            if (overridden != null)
                return overridden.Value;

            var (_, person, provider) = FakeStandardsById[standardId];
            return (BuildSegmentJson(standardId, person, provider), "end_turn");
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
        var document = await CreateDocumentAsync(
            context, "https://unreachable.example.test/document.pdf", createStructureMap: true);

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
        var document = await CreateDocumentAsync(
            context, "https://example.test/document.pdf", createStructureMap: true);

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
        // §6) — even though extraction itself completed cleanly across all 3 standard segments,
        // there is nowhere to persist to. This must read as Skipped, not Success, so it is
        // distinguishable from a real ingestion. Uses the full-success responder so the
        // segmentation completeness check doesn't fail the run before this branch is reached.
        var context = GetDbContext();
        var document = await CreateDocumentAsync(
            context, "https://example.test/document.pdf", createStructureMap: true);

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
        handler.RequestBodies.Should().HaveCount(FakeStandards.Length, "one call per standard, no retries needed");
    }

    [Fact]
    public async Task ExecuteAsync_NoStructureMap_SetsStatusFailedWithNoStructureMap()
    {
        // A document with an active profile but NO registered structure map must fail loudly,
        // before any Claude call is even attempted — there is nothing to drive extraction or
        // completeness-checking against. This is the "no_structure_map" first-class failure
        // state (IRegulatoryStructureMapProvider / RegulatoryStructureMapNotFoundException).
        var context = GetDbContext();
        var document = await CreateDocumentWithProfileAsync(
            context, "https://example.test/document.pdf", createStructureMap: false);

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = false,
            NextExtractedText = "This regulation requires staff to complete manual handling training annually."
        };

        var (job, handler) = BuildJobWithHandler(context, fakePdf, responder: BuildSegmentedResponder());
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Failed);
        reloaded.LastIngestionErrorCode.Should().Be(RegulatoryStructureMapNotFoundException.ErrorCode);
        reloaded.LastIngestionErrorMessage.Should().NotBeNullOrWhiteSpace();
        handler.RequestBodies.Should().BeEmpty("no Claude call should be attempted when there is no structure map to drive extraction");

        var profile = await context.RegulatoryProfiles.FirstAsync(p => p.RegulatoryDocumentId == document.Id);
        var persistedCount = await context.RegulatoryRequirements
            .CountAsync(r => r.RegulatoryProfileId == profile.Id);
        persistedCount.Should().Be(0);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Map-driven segmented extraction (docs/faithful-extraction-build-recon.md)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_AllStandardsSucceed_SetsStatusSuccessAndPersistsCoherentDisplayOrder()
    {
        // Full 3-standard success: each standard's segment returns exactly its declared features
        // (2+1 + 1+2 + 1+1 = 8 total). Confirms both the happy path and that DisplayOrder is
        // assigned coherently across all 3 segments at assembly (no restarts-at-1 collisions
        // between standards) — the map's own principle -> standard -> feature order, not
        // per-call model output, drives DisplayOrder.
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

        handler.RequestBodies.Should().HaveCount(FakeStandards.Length, "one call per standard, no retries needed");

        var profile = await context.RegulatoryProfiles.FirstAsync(p => p.RegulatoryDocumentId == document.Id);
        var persisted = await context.RegulatoryRequirements
            .Where(r => r.RegulatoryProfileId == profile.Id)
            .OrderBy(r => r.DisplayOrder)
            .ToListAsync();

        var expectedTotal = AllFakeFeatureKeys.Count;
        persisted.Should().HaveCount(expectedTotal);

        // Coherent, non-colliding: exactly 1..N once each, no restarts per segment.
        persisted.Select(r => r.DisplayOrder).Should().BeEquivalentTo(
            Enumerable.Range(1, expectedTotal), options => options.WithoutStrictOrdering());
        persisted.Select(r => r.DisplayOrder).Should().OnlyHaveUniqueItems();

        // Assembled in map order (Standard 1.1's 3 features, then 1.2's 3, then 2.1's 2).
        persisted[0].Section.Should().Be("Standard 1.1");
        persisted[2].Section.Should().Be("Standard 1.1");
        persisted[3].Section.Should().Be("Standard 1.2");
        persisted[^1].Section.Should().Be("Standard 2.1");

        // Every persisted requirement is unprovisional (map was Verified) and carries the
        // structural identity fields the map declared.
        persisted.Should().OnlyContain(r => !r.IsProvisional);
        persisted.Should().OnlyContain(r => r.FeatureIdentifier != null && r.Block != null);
    }

    [Fact]
    public async Task ExecuteAsync_MapDeclaredFeatureSet_IsPersistedExactly()
    {
        // Demonstrates the target is MAP-DRIVEN, not model-judged: the persisted set of
        // (FeatureIdentifier, Block) pairs must equal exactly the fake map's declared set,
        // regardless of what the model happens to say — the model can only supply verbatim text
        // for features the map already named; it cannot add or remove from the set.
        var context = GetDbContext();
        var document = await CreateDocumentWithProfileAsync(context, "https://example.test/document.pdf");

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = false,
            NextExtractedText = "This regulation requires staff to complete manual handling training annually."
        };

        var (job, _) = BuildJobWithHandler(context, fakePdf, responder: BuildSegmentedResponder());
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var profile = await context.RegulatoryProfiles.FirstAsync(p => p.RegulatoryDocumentId == document.Id);
        var persisted = await context.RegulatoryRequirements
            .Where(r => r.RegulatoryProfileId == profile.Id)
            .ToListAsync();

        persisted.Select(r => (r.FeatureIdentifier!, r.Block!.Value))
            .Should().BeEquivalentTo(AllFakeFeatureKeys, "the persisted feature set is fixed by the map, not by model judgment");

        // VerbatimText comes from the model's transcription of the freshly-fetched document, not
        // a copy of the map's own ground-truth text — proves the model actually did the
        // transcription work rather than the map's content being echoed straight through.
        var footnoted = persisted.Single(r => r.FeatureIdentifier == FootnotedIdentifier && r.Block == RequirementBlock.Person);
        footnoted.VerbatimText.Should().Be($"Verbatim text for {FootnotedStandardId} person {FootnotedIdentifier}.");
        footnoted.VerbatimText.Should().NotBe($"Map ground truth for Person {FootnotedIdentifier}.");

        // FootnoteDefinition is attached FROM THE MAP (authored data), never from the model —
        // the extraction schema doesn't even have a footnote field.
        footnoted.FootnoteDefinition.Should().Be(FootnoteText);

        // Every other feature (no footnote declared in the map) has none persisted either.
        persisted.Where(r => !(r.FeatureIdentifier == FootnotedIdentifier && r.Block == RequirementBlock.Person))
            .Should().OnlyContain(r => r.FootnoteDefinition == null);
    }

    [Fact]
    public async Task ExecuteAsync_DraftMap_PersistsRequirementsFlaggedProvisional()
    {
        // A Draft (unverified) structure map does not block extraction, but every requirement it
        // produces must be visibly flagged as provisional — the structure it transcribed against
        // has not yet been human-confirmed.
        var context = GetDbContext();
        var document = await CreateDocumentWithProfileAsync(
            context, "https://example.test/document.pdf", mapStatus: RegulatoryStructureMapStatus.Draft);

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = false,
            NextExtractedText = "This regulation requires staff to complete manual handling training annually."
        };

        var (job, _) = BuildJobWithHandler(context, fakePdf, responder: BuildSegmentedResponder());
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);
        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Success, "a Draft map does not block extraction");

        var profile = await context.RegulatoryProfiles.FirstAsync(p => p.RegulatoryDocumentId == document.Id);
        var persisted = await context.RegulatoryRequirements
            .Where(r => r.RegulatoryProfileId == profile.Id)
            .ToListAsync();

        persisted.Should().HaveCount(AllFakeFeatureKeys.Count);
        persisted.Should().OnlyContain(r => r.IsProvisional, "every requirement extracted against a Draft map must be flagged provisional");
    }

    [Fact]
    public async Task ExecuteAsync_TruncatedSegmentRetriesAndSucceeds_SetsStatusSuccess()
    {
        // Standard 1.2's first attempt truncates (stop_reason=max_tokens); the retry succeeds.
        // Every other standard succeeds on its first attempt. Confirms the per-segment retry is
        // scoped to the failing segment only.
        var context = GetDbContext();
        var document = await CreateDocumentWithProfileAsync(context, "https://example.test/document.pdf");

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = false,
            NextExtractedText = "This regulation requires staff to complete manual handling training annually."
        };

        var responder = BuildSegmentedResponder((standardId, isRetry) =>
            standardId == "1.2" && !isRetry
                ? ("[{\"identifier\": \"1.2.1\", \"block\": \"Person\", \"verbatimTex", "max_tokens")
                : null);

        var (job, handler) = BuildJobWithHandler(context, fakePdf, responder: responder);
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);
        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Success);

        // 1 (1.1) + 2 (1.2 attempt + retry) + 1 (2.1) = 4 total Claude calls.
        handler.RequestBodies.Should().HaveCount(4);

        var profile = await context.RegulatoryProfiles.FirstAsync(p => p.RegulatoryDocumentId == document.Id);
        var persistedCount = await context.RegulatoryRequirements
            .CountAsync(r => r.RegulatoryProfileId == profile.Id);
        persistedCount.Should().Be(AllFakeFeatureKeys.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ClaudeResponseTruncated_SetsStatusFailedWithExtractionTruncated()
    {
        // stop_reason=max_tokens on both the initial attempt and the retry must be treated as a
        // failure outright — not a parseable-or-not question. Under run-all-collect-failures,
        // Standard 1.1 (first in map order) fails both attempts, but 1.2 and 2.1 are still
        // attempted and succeed individually — nothing is persisted regardless (all-or-nothing
        // rule), but every standard got a chance to run rather than the loop stopping at the
        // first failure.
        var context = GetDbContext();
        var document = await CreateDocumentWithProfileAsync(context, "https://example.test/document.pdf");

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = false,
            NextExtractedText = "This regulation requires staff to complete manual handling training annually."
        };

        var responder = BuildSegmentedResponder((standardId, _) =>
            standardId == "1.1"
                ? ("[{\"identifier\": \"1.1.1\", \"block\": \"Person\", \"verbatimTex", "max_tokens")
                : null);

        var (job, handler) = BuildJobWithHandler(context, fakePdf, responder: responder);
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Failed);
        reloaded.LastIngestionErrorCode.Should().Be("extraction_truncated");
        reloaded.LastIngestionErrorMessage.Should().NotBeNullOrWhiteSpace();

        // 2 (1.1 attempt + retry, both truncated) + 1 (1.2) + 1 (2.1) = 4 total Claude calls.
        // All 3 standards are attempted even though Standard 1.1 already failed.
        handler.RequestBodies.Should().HaveCount(4, "run-all-collect-failures attempts every standard, not just the first failure");

        var profile = await context.RegulatoryProfiles.FirstAsync(p => p.RegulatoryDocumentId == document.Id);
        var persistedCount = await context.RegulatoryRequirements
            .CountAsync(r => r.RegulatoryProfileId == profile.Id);
        persistedCount.Should().Be(0, "all-or-nothing: nothing may persist when any segment fails");
    }

    [Fact]
    public async Task ExecuteAsync_SegmentMissingDeclaredFeatures_SetsStatusFailedWithExtractionIncompleteNamingMissingFeatures()
    {
        // Standard 1.1's segment returns valid, parseable, non-truncated JSON — but it's short:
        // only Person 1.1.1 out of its 3 declared features (missing Person 1.1.2 and Provider
        // 1.1.1). This is the completeness-check failure mode: a segment can look fine and still
        // have silently dropped content. The missing features must be NAMED specifically in the
        // failure, not a vague shortfall. Both the initial attempt and the retry return the same
        // partial set, so the segment fails after retry; the whole document fails and nothing is
        // persisted. Under run-all-collect-failures, Standards 1.2 and 2.1 are still attempted
        // (and succeed) even though Standard 1.1 already failed.
        var context = GetDbContext();
        var document = await CreateDocumentWithProfileAsync(context, "https://example.test/document.pdf");

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = false,
            NextExtractedText = "This regulation requires staff to complete manual handling training annually."
        };

        var responder = BuildSegmentedResponder((standardId, _) =>
            standardId == "1.1"
                ? (BuildSegmentJson("1.1", new[] { "1.1.1" }, Array.Empty<string>()), "end_turn")
                : null);

        var (job, handler) = BuildJobWithHandler(context, fakePdf, responder: responder);
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Failed);
        reloaded.LastIngestionErrorCode.Should().Be("extraction_incomplete");
        reloaded.LastIngestionErrorMessage.Should().NotBeNullOrWhiteSpace();
        reloaded.LastIngestionErrorMessage.Should().Contain("Person 1.1.2").And.Contain("Provider 1.1.1");

        // 2 (1.1 attempt + retry, both missing features) + 1 (1.2) + 1 (2.1) = 4 total calls.
        // All 3 standards are attempted even though Standard 1.1 already failed.
        handler.RequestBodies.Should().HaveCount(4, "run-all-collect-failures attempts every standard, not just the first failure");

        var profile = await context.RegulatoryProfiles.FirstAsync(p => p.RegulatoryDocumentId == document.Id);
        var persistedCount = await context.RegulatoryRequirements
            .CountAsync(r => r.RegulatoryProfileId == profile.Id);
        persistedCount.Should().Be(0, "all-or-nothing: nothing may persist when any segment fails its completeness check");
    }

    [Fact]
    public async Task ExecuteAsync_TwoStandardsFailDifferentReasons_AggregatesBothInFailureMessage()
    {
        // Standard 1.2 truncates on both attempts; Standard 2.1 is missing a declared feature on
        // both attempts. Two different failure reasons for two different standards — the
        // aggregated failure must name both, not just the first one encountered, and must still
        // attempt every standard (Standard 1.1 succeeds normally).
        var context = GetDbContext();
        var document = await CreateDocumentWithProfileAsync(context, "https://example.test/document.pdf");

        var fakePdf = new FakePdfExtractionService
        {
            ShouldFail = false,
            NextExtractedText = "This regulation requires staff to complete manual handling training annually."
        };

        var responder = BuildSegmentedResponder((standardId, _) => standardId switch
        {
            "1.2" => ("[{\"identifier\": \"1.2.1\", \"block\": \"Person\", \"verbatimTex", "max_tokens"),
            "2.1" => (BuildSegmentJson("2.1", Array.Empty<string>(), new[] { "2.1.1" }), "end_turn"), // missing Person 2.1.1
            _ => null
        });

        var (job, handler) = BuildJobWithHandler(context, fakePdf, responder: responder);
        await job.ExecuteAsync(document.Id, CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Failed);
        reloaded.LastIngestionErrorMessage.Should().NotBeNullOrWhiteSpace();
        reloaded.LastIngestionErrorMessage.Should().Contain("Standard 1.2").And.Contain("Standard 2.1");
        reloaded.LastIngestionErrorMessage.Should().Contain("max_tokens");
        reloaded.LastIngestionErrorMessage.Should().Contain("Person 2.1.1");

        // 1 (1.1) + 2 (1.2 attempt + retry) + 2 (2.1 attempt + retry) = 5 total Claude calls.
        // Every standard is attempted even though two of them fail for different reasons.
        handler.RequestBodies.Should().HaveCount(5, "run-all-collect-failures attempts every standard regardless of earlier failures");

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
        // [AutomaticRetry(Attempts = 1)] can actually see the failure and fire.
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
        // safely rather than reaching HttpClient with a "file://" URI. No structure map is
        // needed here — this fails before the structure map is even resolved.
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
