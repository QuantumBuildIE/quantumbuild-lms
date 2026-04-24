using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Hubs;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Runs the translation validation pipeline against an AuditCorpus without persisting results
/// to the normal validation tables. Results are stored in CorpusRunResult.
/// </summary>
public class CorpusRunJob
{
    private readonly ITranslationValidationService _validationService;
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly IHubContext<CorpusRunHub> _hubContext;
    private readonly IPipelineVersionService _pipelineVersionService;
    private readonly IOptions<TranslationValidationSettings> _settings;
    private readonly ILogger<CorpusRunJob> _logger;

    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CorpusRunJob(
        ITranslationValidationService validationService,
        IToolboxTalksDbContext dbContext,
        IHubContext<CorpusRunHub> hubContext,
        IPipelineVersionService pipelineVersionService,
        IOptions<TranslationValidationSettings> settings,
        ILogger<CorpusRunJob> logger)
    {
        _validationService = validationService;
        _dbContext = dbContext;
        _hubContext = hubContext;
        _pipelineVersionService = pipelineVersionService;
        _settings = settings;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 1)]
    [Queue("content-generation")]
    public async Task ExecuteAsync(
        Guid corpusRunId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "CorpusRunJob started. CorpusRunId={RunId}, TenantId={TenantId}",
            corpusRunId, tenantId);

        try
        {
            // Load the run with corpus and entries
            var run = await _dbContext.CorpusRuns
                .Include(r => r.AuditCorpus)
                    .ThenInclude(c => c.Entries)
                .FirstOrDefaultAsync(r => r.Id == corpusRunId && r.TenantId == tenantId,
                    cancellationToken);

            if (run == null)
            {
                _logger.LogError("CorpusRun {RunId} not found for tenant {TenantId}", corpusRunId, tenantId);
                return;
            }

            // Mark running
            run.Status = CorpusRunStatus.Running;
            run.StartedAt = DateTimeOffset.UtcNow;

            var pipelineVersion = await _pipelineVersionService.GetActiveAsync(cancellationToken);
            if (pipelineVersion != null)
                run.PipelineVersionId = pipelineVersion.Id;

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Determine entries to process
            var allActive = run.AuditCorpus.Entries
                .Where(e => e.IsActive)
                .OrderBy(e => e.EntryRef)
                .ToList();

            var entries = run.IsSmokeTest
                ? allActive.Take(5).ToList()
                : allActive;

            run.TotalEntries = entries.Count;
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (entries.Count == 0)
            {
                _logger.LogWarning("CorpusRun {RunId} has no active entries", corpusRunId);
                await FinishRunAsync(run, [], tenantId, cancellationToken);
                return;
            }

            // Load glossary terms for the corpus sector + language
            var (sourceLanguage, targetLanguage) = ParseLanguagePair(run.AuditCorpus.LanguagePair);
            var glossaryTerms = await LoadGlossaryTermsAsync(
                run.AuditCorpus.SectorKey, tenantId, targetLanguage, cancellationToken);

            // Find most recent previous run for score delta calculation
            var previousRun = await _dbContext.CorpusRuns
                .Where(r => r.CorpusId == run.CorpusId
                    && r.Id != run.Id
                    && r.Status == CorpusRunStatus.Completed
                    && r.TenantId == tenantId)
                .OrderByDescending(r => r.CompletedAt)
                .Select(r => new { r.Id })
                .FirstOrDefaultAsync(cancellationToken);

            Dictionary<Guid, int>? previousScores = null;
            if (previousRun != null)
            {
                previousScores = await _dbContext.CorpusRunResults
                    .Where(r => r.CorpusRunId == previousRun.Id)
                    .ToDictionaryAsync(r => r.CorpusEntryId, r => r.FinalScore, cancellationToken);
            }

            var results = new List<CorpusRunResult>();

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Check provider cache for each provider
                    var cachedResults = await LoadProviderCacheAsync(
                        entry.Id, pipelineVersion?.Version ?? string.Empty, cancellationToken);

                    // Run the dry-run pipeline (persist = false)
                    var validationResult = await _validationService.ValidateSectionAsync(
                        validationRunId: Guid.Empty,
                        sectionIndex: i,
                        sectionTitle: entry.SectionTitle,
                        originalText: entry.OriginalText,
                        translatedText: entry.TranslatedText,
                        sourceLanguage: sourceLanguage,
                        targetLanguage: targetLanguage,
                        sectorKey: entry.SectorKey,
                        passThreshold: entry.PassThreshold,
                        cancellationToken: cancellationToken,
                        tenantId: tenantId,
                        persist: false);

                    // Persist new provider results to cache
                    await UpdateProviderCacheAsync(
                        entry.Id, validationResult, pipelineVersion,
                        cachedResults, cancellationToken);

                    // Determine if this is a regression
                    var isRegression = IsRegression(validationResult.Outcome, entry.ExpectedOutcome);
                    int? scoreDelta = previousScores != null && previousScores.TryGetValue(entry.Id, out var prev)
                        ? validationResult.FinalScore - prev
                        : null;

                    var runResult = new CorpusRunResult
                    {
                        Id = Guid.NewGuid(),
                        CorpusRunId = run.Id,
                        CorpusEntryId = entry.Id,
                        FinalScore = validationResult.FinalScore,
                        Outcome = validationResult.Outcome,
                        ExpectedOutcome = entry.ExpectedOutcome,
                        IsRegression = isRegression,
                        ScoreDelta = scoreDelta,
                        RoundsUsed = validationResult.RoundsUsed,
                        IsSafetyCritical = validationResult.IsSafetyCritical,
                        EffectiveThreshold = validationResult.EffectiveThreshold,
                        BackTranslationA = validationResult.BackTranslationA,
                        BackTranslationB = validationResult.BackTranslationB,
                        BackTranslationC = validationResult.BackTranslationC,
                        BackTranslationD = validationResult.BackTranslationD,
                        ScoreA = validationResult.ScoreA,
                        ScoreB = validationResult.ScoreB,
                        ScoreC = validationResult.ScoreC,
                        ScoreD = validationResult.ScoreD,
                        GlossaryCorrectionsJson = validationResult.GlossaryCorrectionsJson,
                        ArtefactsJson = validationResult.ArtefactsJson,
                        ReviewReasonsJson = validationResult.ReviewReasonsJson,
                        WasCached = cachedResults.Any(),
                    };

                    _dbContext.CorpusRunResults.Add(runResult);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    results.Add(runResult);

                    // Send progress to subscribers
                    var groupName = $"corpus-{corpusRunId}";
                    await _hubContext.Clients.Group(groupName).SendAsync(
                        "CorpusRunProgress",
                        new
                        {
                            corpusRunId,
                            entryIndex = i,
                            totalEntries = entries.Count,
                            entryRef = entry.EntryRef,
                            outcome = validationResult.Outcome.ToString(),
                            score = validationResult.FinalScore,
                            isRegression,
                            wasCached = runResult.WasCached,
                        },
                        cancellationToken);

                    _logger.LogInformation(
                        "CorpusRun entry {Ref}: Score={Score}, Outcome={Outcome}, Regression={Reg}",
                        entry.EntryRef, validationResult.FinalScore, validationResult.Outcome, isRegression);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to process corpus entry {Ref} in run {RunId}",
                        entry.EntryRef, corpusRunId);

                    // Store a failed result so the run can still complete
                    var failedResult = new CorpusRunResult
                    {
                        Id = Guid.NewGuid(),
                        CorpusRunId = run.Id,
                        CorpusEntryId = entry.Id,
                        FinalScore = 0,
                        Outcome = ValidationOutcome.Fail,
                        ExpectedOutcome = entry.ExpectedOutcome,
                        IsRegression = IsRegression(ValidationOutcome.Fail, entry.ExpectedOutcome),
                        RoundsUsed = 0,
                        IsSafetyCritical = entry.IsSafetyCritical,
                        EffectiveThreshold = entry.PassThreshold,
                    };

                    _dbContext.CorpusRunResults.Add(failedResult);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    results.Add(failedResult);
                }
            }

            await FinishRunAsync(run, results, tenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CorpusRunJob failed for run {RunId}", corpusRunId);

            try
            {
                var run = await _dbContext.CorpusRuns
                    .FirstOrDefaultAsync(r => r.Id == corpusRunId, CancellationToken.None);

                if (run != null)
                {
                    run.Status = CorpusRunStatus.Failed;
                    run.CompletedAt = DateTimeOffset.UtcNow;
                    run.ErrorMessage = ex.Message;
                    await _dbContext.SaveChangesAsync(CancellationToken.None);
                }

                var groupName = $"corpus-{corpusRunId}";
                await _hubContext.Clients.Group(groupName).SendAsync(
                    "CorpusRunFailed",
                    new { corpusRunId, errorMessage = ex.Message },
                    CancellationToken.None);
            }
            catch (Exception inner)
            {
                _logger.LogError(inner, "Failed to mark corpus run as failed");
            }
        }
    }

    private async Task FinishRunAsync(
        CorpusRun run,
        List<CorpusRunResult> results,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // Aggregate stats
        run.PassedEntries = results.Count(r => r.Outcome == ValidationOutcome.Pass);
        run.ReviewEntries = results.Count(r => r.Outcome == ValidationOutcome.Review);
        run.FailedEntries = results.Count(r => r.Outcome == ValidationOutcome.Fail);
        run.RegressionEntries = results.Count(r => r.IsRegression);
        run.MeanScore = results.Count > 0
            ? Math.Round((decimal)results.Average(r => r.FinalScore), 1)
            : null;

        var regressions = results.Where(r => r.IsRegression && r.ScoreDelta.HasValue).ToList();
        run.MaxScoreDrop = regressions.Count > 0
            ? regressions.Min(r => r.ScoreDelta!.Value) // most negative
            : null;

        // Determine verdict
        if (results.Count == 0)
        {
            run.Verdict = CorpusVerdict.Inconclusive;
        }
        else
        {
            var regressionPercent = (double)run.RegressionEntries / results.Count * 100;
            var maxDrop = run.MaxScoreDrop.HasValue ? Math.Abs(run.MaxScoreDrop.Value) : 0;

            if (regressionPercent > run.FailureThresholdPercent
                || maxDrop > run.ScoreDropThreshold * 2)
            {
                run.Verdict = CorpusVerdict.Fail;
            }
            else if (run.RegressionEntries > 0)
            {
                run.Verdict = CorpusVerdict.Inconclusive;
            }
            else
            {
                run.Verdict = CorpusVerdict.Pass;
            }
        }

        // Handle linked pipeline change
        string? linkedChangeStatus = null;
        if (run.LinkedPipelineChangeId.HasValue)
        {
            var change = await _dbContext.PipelineChangeRecords
                .FirstOrDefaultAsync(c => c.Id == run.LinkedPipelineChangeId.Value, cancellationToken);

            if (change != null)
            {
                if (run.Verdict == CorpusVerdict.Fail)
                {
                    change.Status = PipelineChangeStatus.BlockedRegression;
                    linkedChangeStatus = "BlockedRegression";

                    // Auto-create deviation
                    await CreateRegressionDeviationAsync(run, tenantId, cancellationToken);
                }
                else if (run.Verdict == CorpusVerdict.Pass)
                {
                    change.Status = PipelineChangeStatus.PendingApproval;
                    linkedChangeStatus = "PendingApproval";
                }
            }
        }

        run.Status = CorpusRunStatus.Completed;
        run.CompletedAt = DateTimeOffset.UtcNow;

        // Estimate actual cost from AI usage logs written during this run
        run.ActualCostEur = await EstimateActualCostAsync(run.Id, tenantId, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "CorpusRun {RunId} completed. Verdict={Verdict}, Regressions={Reg}/{Total}, MeanScore={Score}",
            run.Id, run.Verdict, run.RegressionEntries, results.Count, run.MeanScore);

        var group = $"corpus-{run.Id}";
        await _hubContext.Clients.Group(group).SendAsync(
            "CorpusRunComplete",
            new
            {
                corpusRunId = run.Id,
                verdict = run.Verdict?.ToString(),
                meanScore = run.MeanScore,
                maxScoreDrop = run.MaxScoreDrop,
                regressionEntries = run.RegressionEntries,
                totalEntries = results.Count,
                linkedChangeStatus,
            },
            cancellationToken);
    }

    private async Task CreateRegressionDeviationAsync(
        CorpusRun run, Guid tenantId, CancellationToken ct)
    {
        try
        {
            // Generate sequential DeviationId for this tenant
            var maxId = await _dbContext.TranslationDeviations
                .IgnoreQueryFilters()
                .Where(d => d.TenantId == tenantId)
                .OrderByDescending(d => d.DeviationId)
                .Select(d => d.DeviationId)
                .FirstOrDefaultAsync(ct);

            var nextNum = ParseIdNumber(maxId) + 1;
            var deviationId = $"DEV-{nextNum:D3}";

            var activePipeline = await _pipelineVersionService.GetActiveAsync(ct);
            var changeRef = run.LinkedPipelineChangeId.HasValue
                ? run.LinkedPipelineChangeId.Value.ToString("N")[..8]
                : "n/a";

            var metadataJson = JsonSerializer.Serialize(new
            {
                corpusRunId = run.Id,
                linkedPipelineChangeId = run.LinkedPipelineChangeId,
            }, CamelCase);

            var deviation = new TranslationDeviation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DeviationId = deviationId,
                DetectedAt = DateTimeOffset.UtcNow,
                DetectedBy = "CorpusRunJob (automated)",
                Nature = $"Corpus regression detected — {run.RegressionEntries} entries regressed on pipeline change {changeRef}",
                RootCauseCategory = "pipeline",
                RootCauseDetail = metadataJson,
                Status = DeviationStatus.Open,
                PipelineVersionAtTime = activePipeline?.Hash,
            };

            _dbContext.TranslationDeviations.Add(deviation);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Auto-created deviation {DeviationId} for corpus regression in run {RunId}",
                deviationId, run.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-create regression deviation for run {RunId}", run.Id);
        }
    }

    private async Task<decimal?> EstimateActualCostAsync(
        Guid corpusRunId, Guid tenantId, CancellationToken ct)
    {
        try
        {
            var logs = await _dbContext.AiUsageLogs
                .Where(l => l.ReferenceEntityId == corpusRunId && l.TenantId == tenantId)
                .ToListAsync(ct);

            if (logs.Count == 0) return null;

            decimal total = 0m;
            foreach (var log in logs)
            {
                total += log.ModelId switch
                {
                    var m when m.Contains("haiku") =>
                        (log.InputTokens / 1000m) * 0.00074m + (log.OutputTokens / 1000m) * 0.00370m,
                    var m when m.Contains("sonnet") =>
                        (log.InputTokens / 1000m) * 0.00277m + (log.OutputTokens / 1000m) * 0.01385m,
                    var m when m.Contains("gemini") =>
                        (log.InputTokens / 1000m) * 0.00007m + (log.OutputTokens / 1000m) * 0.00028m,
                    _ => 0m,
                };
            }

            return Math.Round(total, 4);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to estimate actual cost for corpus run {RunId}", corpusRunId);
            return null;
        }
    }

    private async Task<List<ProviderResultCache>> LoadProviderCacheAsync(
        Guid entryId, string providerVersion, CancellationToken ct)
    {
        return await _dbContext.ProviderResultCache
            .Where(c => c.CorpusEntryId == entryId
                && c.ProviderVersion == providerVersion)
            .ToListAsync(ct);
    }

    private async Task UpdateProviderCacheAsync(
        Guid entryId,
        TranslationValidationResult result,
        PipelineVersion? pipelineVersion,
        List<ProviderResultCache> existing,
        CancellationToken ct)
    {
        if (pipelineVersion == null) return;

        var providerVersion = pipelineVersion.Version;
        var now = DateTimeOffset.UtcNow;

        void UpsertCache(string provider, string? backTranslation, int? score)
        {
            if (string.IsNullOrWhiteSpace(backTranslation)) return;

            var cached = existing.FirstOrDefault(c => c.Provider == provider);
            if (cached == null)
            {
                _dbContext.ProviderResultCache.Add(new ProviderResultCache
                {
                    Id = Guid.NewGuid(),
                    CorpusEntryId = entryId,
                    Provider = provider,
                    ProviderVersion = providerVersion,
                    BackTranslation = backTranslation,
                    Score = score ?? 0,
                    ComputedAt = now,
                });
            }
        }

        UpsertCache("haiku", result.BackTranslationA, result.ScoreA);
        UpsertCache("deepl", result.BackTranslationB, result.ScoreB);
        UpsertCache("gemini", result.BackTranslationC, result.ScoreC);
        UpsertCache("sonnet", result.BackTranslationD, result.ScoreD);

        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task<List<SafetyGlossaryTerm>> LoadGlossaryTermsAsync(
        string sectorKey, Guid tenantId, string targetLanguage, CancellationToken ct)
    {
        var terms = await _dbContext.SafetyGlossaryTerms
            .IgnoreQueryFilters()
            .Include(t => t.Glossary)
            .Where(t => !t.IsDeleted
                && !t.Glossary.IsDeleted
                && t.Glossary.SectorKey == sectorKey
                && (t.Glossary.TenantId == tenantId || t.Glossary.TenantId == null))
            .ToListAsync(ct);

        return terms;
    }

    private static (string Source, string Target) ParseLanguagePair(string languagePair)
    {
        var parts = languagePair.Split('-', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : ("en", languagePair);
    }

    private static bool IsRegression(ValidationOutcome actual, ValidationOutcome expected)
    {
        // Higher enum value = worse outcome: Pass(0) < Review(1) < Fail(2)
        return (int)actual > (int)expected;
    }

    private static int ParseIdNumber(string? id)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        var parts = id.Split('-');
        return parts.Length > 1 && int.TryParse(parts[^1], out var n) ? n : 0;
    }
}
