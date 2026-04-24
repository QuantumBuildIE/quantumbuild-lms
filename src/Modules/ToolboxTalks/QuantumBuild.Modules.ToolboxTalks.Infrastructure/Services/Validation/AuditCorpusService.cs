using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

public class AuditCorpusService : IAuditCorpusService
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ICostEstimationService _costEstimation;
    private readonly IPipelineVersionService _pipelineVersionService;
    private readonly IOptions<TranslationValidationSettings> _settings;
    private readonly ILogger<AuditCorpusService> _logger;

    private static readonly TimeSpan RunCooldown = TimeSpan.FromMinutes(10);

    public AuditCorpusService(
        IToolboxTalksDbContext dbContext,
        ICurrentUserService currentUser,
        ICostEstimationService costEstimation,
        IPipelineVersionService pipelineVersionService,
        IOptions<TranslationValidationSettings> settings,
        ILogger<AuditCorpusService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _costEstimation = costEstimation;
        _pipelineVersionService = pipelineVersionService;
        _settings = settings;
        _logger = logger;
    }

    public async Task<AuditCorpus> FreezeFromTalkAsync(
        Guid talkId,
        string name,
        string? description,
        IEnumerable<int> sectionIndexes,
        CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;
        var indexSet = sectionIndexes.ToHashSet();

        var talk = await _dbContext.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == talkId && t.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Talk {talkId} not found");

        // Load sections with accepted validation results
        var sections = await _dbContext.ToolboxTalkSections
            .Where(s => s.ToolboxTalkId == talkId)
            .OrderBy(s => s.SectionNumber)
            .ToListAsync(ct);

        // Find the most recent completed run for this talk to get accepted sections
        var latestRun = await _dbContext.TranslationValidationRuns
            .Where(r => r.ToolboxTalkId == talkId
                && r.TenantId == tenantId
                && r.Status == ValidationRunStatus.Completed)
            .OrderByDescending(r => r.CompletedAt)
            .FirstOrDefaultAsync(ct);

        var acceptedResults = latestRun != null
            ? await _dbContext.TranslationValidationResults
                .Where(r => r.ValidationRunId == latestRun.Id
                    && r.ReviewerDecision == ReviewerDecision.Accepted)
                .ToListAsync(ct)
            : new List<TranslationValidationResult>();

        var eligibleResults = acceptedResults
            .Where(r => indexSet.Count == 0 || indexSet.Contains(r.SectionIndex))
            .ToList();

        if (eligibleResults.Count == 0)
            throw new InvalidOperationException(
                "No accepted translation results found for the selected section indexes");

        var pipelineVersion = await _pipelineVersionService.GetActiveAsync(ct);
        var languagePair = latestRun != null
            ? $"{latestRun.SourceLanguage}-{latestRun.LanguageCode}"
            : "en-??";

        var corpusId = await GenerateCorpusIdAsync(tenantId, ct);

        var corpus = new AuditCorpus
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CorpusId = corpusId,
            Name = name,
            Description = description,
            SectorKey = latestRun?.SectorKey ?? talk.Category ?? "general",
            LanguagePair = languagePair,
            SourceTalkId = talkId,
            FrozenFromPipelineVersionId = pipelineVersion?.Id,
            IsLocked = false,
            Version = 1,
            CreatedBy = _currentUser.UserName ?? "system",
            CreatedAt = DateTime.UtcNow,
        };

        // Build entries from accepted results
        var entryNum = 1;
        foreach (var result in eligibleResults.OrderBy(r => r.SectionIndex))
        {
            var entry = new AuditCorpusEntry
            {
                Id = Guid.NewGuid(),
                CorpusId = corpus.Id,
                EntryRef = $"{corpusId}-E{entryNum:D2}",
                SectionTitle = result.SectionTitle,
                OriginalText = result.OriginalText,
                TranslatedText = result.EditedTranslation ?? result.TranslatedText,
                SourceLanguage = latestRun?.SourceLanguage ?? "en",
                TargetLanguage = latestRun?.LanguageCode ?? "??",
                SectorKey = corpus.SectorKey,
                PassThreshold = latestRun?.PassThreshold ?? _settings.Value.DefaultThreshold,
                ExpectedOutcome = result.Outcome,
                IsSafetyCritical = result.IsSafetyCritical,
                PipelineVersionIdAtFreeze = pipelineVersion?.Id,
                IsActive = true,
                CreatedBy = _currentUser.UserName ?? "system",
                CreatedAt = DateTime.UtcNow,
            };

            corpus.Entries.Add(entry);
            entryNum++;
        }

        _dbContext.AuditCorpora.Add(corpus);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Corpus {CorpusId} frozen from talk {TalkId} with {Count} entries",
            corpusId, talkId, corpus.Entries.Count);

        return corpus;
    }

    public async Task<AuditCorpus> LockCorpusAsync(
        Guid corpusId, string signedBy, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;

        var corpus = await _dbContext.AuditCorpora
            .Include(c => c.Entries)
            .FirstOrDefaultAsync(c => c.Id == corpusId && c.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Corpus {corpusId} not found");

        if (corpus.IsLocked)
            throw new InvalidOperationException("Corpus is already locked");

        corpus.IsLocked = true;
        corpus.LockedAt = DateTimeOffset.UtcNow;
        corpus.LockedBy = _currentUser.UserName;
        corpus.SignedBy = signedBy;
        corpus.Version += 1;
        corpus.UpdatedAt = DateTime.UtcNow;
        corpus.UpdatedBy = _currentUser.UserName;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Corpus {CorpusId} locked by {User}", corpus.CorpusId, signedBy);

        return corpus;
    }

    public async Task<AuditCorpus> AddEntryAsync(
        Guid corpusId, AddCorpusEntryRequest request, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;

        var corpus = await _dbContext.AuditCorpora
            .Include(c => c.Entries)
            .FirstOrDefaultAsync(c => c.Id == corpusId && c.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Corpus {corpusId} not found");

        if (corpus.IsLocked)
            throw new InvalidOperationException("Cannot add entries to a locked corpus");

        // Generate sequential entry ref
        var existingNums = corpus.Entries
            .Select(e => ParseEntryNum(e.EntryRef))
            .Where(n => n > 0)
            .ToList();

        var nextNum = existingNums.Count > 0 ? existingNums.Max() + 1 : 1;

        var entry = new AuditCorpusEntry
        {
            Id = Guid.NewGuid(),
            CorpusId = corpus.Id,
            EntryRef = $"{corpus.CorpusId}-E{nextNum:D2}",
            SectionTitle = request.SectionTitle,
            OriginalText = request.OriginalText,
            TranslatedText = request.TranslatedText,
            SourceLanguage = request.SourceLanguage,
            TargetLanguage = request.TargetLanguage,
            SectorKey = corpus.SectorKey,
            PassThreshold = request.PassThreshold,
            ExpectedOutcome = request.ExpectedOutcome,
            IsSafetyCritical = request.IsSafetyCritical,
            TagsJson = request.TagsJson,
            IsActive = true,
            CreatedBy = _currentUser.UserName ?? "system",
            CreatedAt = DateTime.UtcNow,
        };

        corpus.Entries.Add(entry);
        corpus.Version += 1;
        corpus.UpdatedAt = DateTime.UtcNow;
        corpus.UpdatedBy = _currentUser.UserName;

        await _dbContext.SaveChangesAsync(ct);

        return corpus;
    }

    public async Task RemoveEntryAsync(Guid corpusId, Guid entryId, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;

        var corpus = await _dbContext.AuditCorpora
            .Include(c => c.Entries)
            .FirstOrDefaultAsync(c => c.Id == corpusId && c.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Corpus {corpusId} not found");

        if (corpus.IsLocked)
            throw new InvalidOperationException("Cannot remove entries from a locked corpus");

        var entry = corpus.Entries.FirstOrDefault(e => e.Id == entryId)
            ?? throw new InvalidOperationException($"Entry {entryId} not found");

        entry.IsActive = false;
        entry.UpdatedAt = DateTime.UtcNow;
        entry.UpdatedBy = _currentUser.UserName;

        corpus.Version += 1;
        corpus.UpdatedAt = DateTime.UtcNow;
        corpus.UpdatedBy = _currentUser.UserName;

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<(CorpusRun Run, decimal EstimatedCostEur)> PrepareRunAsync(
        Guid corpusId,
        bool isSmokeTest,
        CorpusTriggerType triggerType,
        Guid? linkedPipelineChangeId,
        CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;

        var corpus = await _dbContext.AuditCorpora
            .Include(c => c.Entries)
            .FirstOrDefaultAsync(c => c.Id == corpusId && c.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Corpus {corpusId} not found");

        // Cooldown check
        var recentRun = await _dbContext.CorpusRuns
            .Where(r => r.CorpusId == corpusId
                && r.TenantId == tenantId
                && r.CreatedAt >= DateTime.UtcNow.Subtract(RunCooldown))
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (recentRun != null)
            throw new InvalidOperationException(
                "A run was triggered for this corpus within the last 10 minutes. Please wait before re-triggering.");

        // Check no existing pending/running run
        var activeRun = await _dbContext.CorpusRuns
            .Where(r => r.CorpusId == corpusId
                && r.TenantId == tenantId
                && (r.Status == CorpusRunStatus.Pending || r.Status == CorpusRunStatus.Running))
            .FirstOrDefaultAsync(ct);

        if (activeRun != null)
            throw new InvalidOperationException(
                "A run is already pending or running for this corpus");

        var activeEntries = corpus.Entries.Where(e => e.IsActive).ToList();
        var entriesToEstimate = isSmokeTest ? activeEntries.Take(5) : activeEntries;

        var estimatedCost = _costEstimation.EstimateCorpusRunCostEur(
            entriesToEstimate, _settings.Value.MaxRounds, isSmokeTest);

        var pipelineVersion = await _pipelineVersionService.GetActiveAsync(ct);

        var run = new CorpusRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CorpusId = corpus.Id,
            PipelineVersionId = pipelineVersion?.Id,
            LinkedPipelineChangeId = linkedPipelineChangeId,
            TriggerType = triggerType,
            TriggeredBy = _currentUser.UserName,
            IsSmokeTest = isSmokeTest,
            Status = CorpusRunStatus.Pending,
            TotalEntries = isSmokeTest ? Math.Min(5, activeEntries.Count) : activeEntries.Count,
            FailureThresholdPercent = 20,
            ScoreDropThreshold = 10,
            EstimatedCostEur = estimatedCost,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName ?? "system",
        };

        _dbContext.CorpusRuns.Add(run);
        await _dbContext.SaveChangesAsync(ct);

        return (run, estimatedCost);
    }

    public async Task<CorpusRun> EnqueueRunAsync(Guid corpusRunId, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;

        var run = await _dbContext.CorpusRuns
            .FirstOrDefaultAsync(r => r.Id == corpusRunId && r.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"CorpusRun {corpusRunId} not found");

        if (run.Status != CorpusRunStatus.Pending)
            throw new InvalidOperationException($"Run {corpusRunId} is not in Pending status");

        BackgroundJob.Enqueue<CorpusRunJob>(
            job => job.ExecuteAsync(run.Id, tenantId, CancellationToken.None));

        _logger.LogInformation("CorpusRun {RunId} enqueued for corpus {CorpusId}", run.Id, run.CorpusId);

        return run;
    }

    public async Task<CorpusRun> TriggerRunAsync(
        Guid corpusId,
        bool isSmokeTest,
        CorpusTriggerType triggerType,
        Guid? linkedPipelineChangeId,
        CancellationToken ct = default)
    {
        var (run, _) = await PrepareRunAsync(corpusId, isSmokeTest, triggerType, linkedPipelineChangeId, ct);
        return await EnqueueRunAsync(run.Id, ct);
    }

    public async Task<PaginatedList<AuditCorpusDto>> GetPagedAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;

        var query = _dbContext.AuditCorpora
            .Include(c => c.Entries)
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.CreatedAt);

        var projected = query.Select(c => new AuditCorpusDto
        {
            Id = c.Id,
            CorpusId = c.CorpusId,
            Name = c.Name,
            Description = c.Description,
            SectorKey = c.SectorKey,
            LanguagePair = c.LanguagePair,
            SourceTalkId = c.SourceTalkId,
            FrozenFromPipelineVersionId = c.FrozenFromPipelineVersionId,
            IsLocked = c.IsLocked,
            LockedAt = c.LockedAt,
            LockedBy = c.LockedBy,
            SignedBy = c.SignedBy,
            Version = c.Version,
            EntryCount = c.Entries.Count,
            ActiveEntryCount = c.Entries.Count(e => e.IsActive),
            CreatedAt = c.CreatedAt,
        });

        return await PaginatedList<AuditCorpusDto>.CreateAsync(projected, page, pageSize);
    }

    public async Task<AuditCorpusDto?> GetByIdAsync(Guid corpusId, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;

        var corpus = await _dbContext.AuditCorpora
            .Include(c => c.Entries)
            .Include(c => c.SourceTalk)
            .Include(c => c.FrozenFromPipelineVersion)
            .FirstOrDefaultAsync(c => c.Id == corpusId && c.TenantId == tenantId, ct);

        if (corpus == null) return null;

        // Get last run
        var lastRun = await _dbContext.CorpusRuns
            .Where(r => r.CorpusId == corpus.Id && r.TenantId == tenantId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new CorpusRunSummaryDto
            {
                Id = r.Id,
                Status = r.Status,
                TriggerType = r.TriggerType,
                TriggeredBy = r.TriggeredBy,
                IsSmokeTest = r.IsSmokeTest,
                TotalEntries = r.TotalEntries,
                RegressionEntries = r.RegressionEntries,
                MeanScore = r.MeanScore,
                MaxScoreDrop = r.MaxScoreDrop,
                Verdict = r.Verdict,
                EstimatedCostEur = r.EstimatedCostEur,
                ActualCostEur = r.ActualCostEur,
                StartedAt = r.StartedAt,
                CompletedAt = r.CompletedAt,
                CreatedAt = r.CreatedAt,
            })
            .FirstOrDefaultAsync(ct);

        return new AuditCorpusDto
        {
            Id = corpus.Id,
            CorpusId = corpus.CorpusId,
            Name = corpus.Name,
            Description = corpus.Description,
            SectorKey = corpus.SectorKey,
            LanguagePair = corpus.LanguagePair,
            SourceTalkId = corpus.SourceTalkId,
            SourceTalkTitle = corpus.SourceTalk?.Title,
            FrozenFromPipelineVersionId = corpus.FrozenFromPipelineVersionId,
            FrozenFromPipelineHash = corpus.FrozenFromPipelineVersion?.Hash,
            IsLocked = corpus.IsLocked,
            LockedAt = corpus.LockedAt,
            LockedBy = corpus.LockedBy,
            SignedBy = corpus.SignedBy,
            Version = corpus.Version,
            EntryCount = corpus.Entries.Count,
            ActiveEntryCount = corpus.Entries.Count(e => e.IsActive),
            LastRun = lastRun,
            CreatedAt = corpus.CreatedAt,
        };
    }

    public async Task<PaginatedList<CorpusRunSummaryDto>> GetRunsAsync(
        Guid corpusId, int page, int pageSize, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;

        var query = _dbContext.CorpusRuns
            .Where(r => r.CorpusId == corpusId && r.TenantId == tenantId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new CorpusRunSummaryDto
            {
                Id = r.Id,
                Status = r.Status,
                TriggerType = r.TriggerType,
                TriggeredBy = r.TriggeredBy,
                IsSmokeTest = r.IsSmokeTest,
                TotalEntries = r.TotalEntries,
                RegressionEntries = r.RegressionEntries,
                MeanScore = r.MeanScore,
                MaxScoreDrop = r.MaxScoreDrop,
                Verdict = r.Verdict,
                EstimatedCostEur = r.EstimatedCostEur,
                ActualCostEur = r.ActualCostEur,
                StartedAt = r.StartedAt,
                CompletedAt = r.CompletedAt,
                CreatedAt = r.CreatedAt,
            });

        return await PaginatedList<CorpusRunSummaryDto>.CreateAsync(query, page, pageSize);
    }

    public async Task<CorpusRunDetailDto?> GetRunDetailAsync(Guid runId, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;

        var run = await _dbContext.CorpusRuns
            .Include(r => r.AuditCorpus)
            .Include(r => r.Results)
                .ThenInclude(res => res.CorpusEntry)
            .Include(r => r.PipelineVersion)
            .Include(r => r.LinkedPipelineChange)
            .FirstOrDefaultAsync(r => r.Id == runId && r.TenantId == tenantId, ct);

        if (run == null) return null;

        return new CorpusRunDetailDto
        {
            Id = run.Id,
            CorpusId = run.CorpusId,
            CorpusName = run.AuditCorpus.Name,
            Status = run.Status,
            TriggerType = run.TriggerType,
            TriggeredBy = run.TriggeredBy,
            IsSmokeTest = run.IsSmokeTest,
            TotalEntries = run.TotalEntries,
            PassedEntries = run.PassedEntries,
            ReviewEntries = run.ReviewEntries,
            FailedEntries = run.FailedEntries,
            RegressionEntries = run.RegressionEntries,
            MeanScore = run.MeanScore,
            MaxScoreDrop = run.MaxScoreDrop,
            Verdict = run.Verdict,
            FailureThresholdPercent = run.FailureThresholdPercent,
            ScoreDropThreshold = run.ScoreDropThreshold,
            EstimatedCostEur = run.EstimatedCostEur,
            ActualCostEur = run.ActualCostEur,
            ErrorMessage = run.ErrorMessage,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            PipelineVersionId = run.PipelineVersionId,
            PipelineVersionHash = run.PipelineVersion?.Hash,
            LinkedPipelineChangeId = run.LinkedPipelineChangeId,
            LinkedPipelineChangeStatus = run.LinkedPipelineChange?.Status.ToString(),
            Results = run.Results
                .OrderBy(r => r.CorpusEntry.EntryRef)
                .Select(r => new CorpusRunResultDto
                {
                    Id = r.Id,
                    CorpusEntryId = r.CorpusEntryId,
                    EntryRef = r.CorpusEntry.EntryRef,
                    SectionTitle = r.CorpusEntry.SectionTitle,
                    FinalScore = r.FinalScore,
                    Outcome = r.Outcome,
                    ExpectedOutcome = r.ExpectedOutcome,
                    IsRegression = r.IsRegression,
                    ScoreDelta = r.ScoreDelta,
                    RoundsUsed = r.RoundsUsed,
                    IsSafetyCritical = r.IsSafetyCritical,
                    EffectiveThreshold = r.EffectiveThreshold,
                    BackTranslationA = r.BackTranslationA,
                    BackTranslationB = r.BackTranslationB,
                    BackTranslationC = r.BackTranslationC,
                    BackTranslationD = r.BackTranslationD,
                    ScoreA = r.ScoreA,
                    ScoreB = r.ScoreB,
                    ScoreC = r.ScoreC,
                    ScoreD = r.ScoreD,
                    GlossaryCorrectionsJson = r.GlossaryCorrectionsJson,
                    ArtefactsJson = r.ArtefactsJson,
                    ReviewReasonsJson = r.ReviewReasonsJson,
                    WasCached = r.WasCached,
                })
                .ToList(),
        };
    }

    public async Task<CorpusRunDiffDto?> GetRunDiffAsync(Guid runId, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;

        var run = await _dbContext.CorpusRuns
            .Include(r => r.Results)
                .ThenInclude(res => res.CorpusEntry)
            .FirstOrDefaultAsync(r => r.Id == runId && r.TenantId == tenantId, ct);

        if (run == null) return null;

        // Find the previous completed run for this corpus
        var previousRun = await _dbContext.CorpusRuns
            .Where(r => r.CorpusId == run.CorpusId
                && r.Id != run.Id
                && r.Status == CorpusRunStatus.Completed
                && r.TenantId == tenantId)
            .OrderByDescending(r => r.CompletedAt)
            .Select(r => new { r.Id })
            .FirstOrDefaultAsync(ct);

        Dictionary<Guid, (int Score, ValidationOutcome Outcome)> previousScores = new();
        if (previousRun != null)
        {
            previousScores = await _dbContext.CorpusRunResults
                .Where(r => r.CorpusRunId == previousRun.Id)
                .ToDictionaryAsync(r => r.CorpusEntryId, r => (r.FinalScore, r.Outcome), ct);
        }

        var regressions = run.Results
            .Where(r => r.IsRegression)
            .OrderBy(r => r.CorpusEntry.EntryRef)
            .Select(r =>
            {
                previousScores.TryGetValue(r.CorpusEntryId, out var prev);
                return new CorpusRunDiffEntry
                {
                    CorpusEntryId = r.CorpusEntryId,
                    EntryRef = r.CorpusEntry.EntryRef,
                    SectionTitle = r.CorpusEntry.SectionTitle,
                    TranslatedText = r.CorpusEntry.TranslatedText,
                    CurrentScore = r.FinalScore,
                    CurrentOutcome = r.Outcome,
                    PreviousScore = prev == default ? null : prev.Score,
                    PreviousOutcome = prev == default ? null : prev.Outcome,
                    ScoreDelta = r.ScoreDelta,
                };
            })
            .ToList();

        return new CorpusRunDiffDto
        {
            RunId = runId,
            PreviousRunId = previousRun?.Id,
            RegressionEntries = regressions,
        };
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private async Task<string> GenerateCorpusIdAsync(Guid tenantId, CancellationToken ct)
    {
        var maxId = await _dbContext.AuditCorpora
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.CorpusId)
            .Select(c => c.CorpusId)
            .FirstOrDefaultAsync(ct);

        var next = ParseIdNumber(maxId, "CORPUS-") + 1;
        return $"CORPUS-{next:D3}";
    }

    private static int ParseIdNumber(string? id, string prefix = "")
    {
        if (string.IsNullOrEmpty(id)) return 0;
        var withoutPrefix = id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? id[prefix.Length..]
            : id;
        var parts = withoutPrefix.Split('-');
        return int.TryParse(parts[^1], out var n) ? n : 0;
    }

    private static int ParseEntryNum(string entryRef)
    {
        // "CORPUS-001-E05" → 5
        var eIdx = entryRef.LastIndexOf("-E", StringComparison.OrdinalIgnoreCase);
        if (eIdx < 0) return 0;
        return int.TryParse(entryRef[(eIdx + 2)..], out var n) ? n : 0;
    }
}
