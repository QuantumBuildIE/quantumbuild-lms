using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

public class PipelineAuditQueryService : IPipelineAuditQueryService
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public PipelineAuditQueryService(
        IToolboxTalksDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<PaginatedList<ModuleOutcomeDto>> GetModuleOutcomesAsync(
        Guid? tenantId,
        ValidationOutcome? outcome,
        string? languageCode,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var effectiveTenantId = ResolveQueryTenantId(tenantId);

        var query = _dbContext.TranslationValidationRuns
            .Include(r => r.Results)
            .Include(r => r.PipelineVersion)
            .Where(r => r.Status == ValidationRunStatus.Completed);

        if (effectiveTenantId.HasValue)
            query = query.Where(r => r.TenantId == effectiveTenantId.Value);

        if (outcome.HasValue)
            query = query.Where(r => r.OverallOutcome == outcome.Value);

        if (!string.IsNullOrEmpty(languageCode))
            query = query.Where(r => r.LanguageCode == languageCode);

        // Load talks and courses for title resolution
        var talksQuery = _dbContext.ToolboxTalks.AsNoTracking();
        var coursesQuery = _dbContext.ToolboxTalkCourses.AsNoTracking();

        var ordered = query.OrderByDescending(r => r.CompletedAt ?? r.CreatedAt);

        var total = await ordered.CountAsync(ct);
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Load titles in batch
        var talkIds = items.Where(r => r.ToolboxTalkId.HasValue).Select(r => r.ToolboxTalkId!.Value).Distinct().ToList();
        var courseIds = items.Where(r => r.CourseId.HasValue).Select(r => r.CourseId!.Value).Distinct().ToList();

        var talkTitles = talkIds.Count > 0
            ? await _dbContext.ToolboxTalks
                .Where(t => talkIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Title })
                .ToDictionaryAsync(t => t.Id, t => t.Title, ct)
            : new Dictionary<Guid, string>();

        var courseTitles = courseIds.Count > 0
            ? await _dbContext.ToolboxTalkCourses
                .Where(c => courseIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Title })
                .ToDictionaryAsync(c => c.Id, c => c.Title, ct)
            : new Dictionary<Guid, string>();

        var dtos = items.Select(r =>
        {
            var accepted = r.Results.Count(res => res.ReviewerDecision == ReviewerDecision.Accepted);
            var rejected = r.Results.Count(res => res.ReviewerDecision == ReviewerDecision.Rejected
                || res.ReviewerDecision == ReviewerDecision.Edited);
            var pending = r.Results.Count(res => res.ReviewerDecision == ReviewerDecision.Pending);
            var isSafetyCritical = r.Results.Any(res => res.IsSafetyCritical);

            return new ModuleOutcomeDto
            {
                RunId = r.Id,
                ToolboxTalkId = r.ToolboxTalkId,
                TalkTitle = r.ToolboxTalkId.HasValue
                    ? talkTitles.GetValueOrDefault(r.ToolboxTalkId.Value)
                    : null,
                CourseId = r.CourseId,
                CourseTitle = r.CourseId.HasValue
                    ? courseTitles.GetValueOrDefault(r.CourseId.Value)
                    : null,
                LanguageCode = r.LanguageCode,
                SectorKey = r.SectorKey,
                OverallScore = r.OverallScore,
                OverallOutcome = r.OverallOutcome,
                IsSafetyCritical = isSafetyCritical,
                TotalSections = r.TotalSections,
                PassedSections = r.PassedSections,
                ReviewSections = r.ReviewSections,
                FailedSections = r.FailedSections,
                CompletedAt = r.CompletedAt,
                PipelineVersionHash = r.PipelineVersion?.Hash,
                AcceptedDecisions = accepted,
                RejectedDecisions = rejected,
                PendingDecisions = pending
            };
        }).ToList();

        return new PaginatedList<ModuleOutcomeDto>(dtos, total, page, pageSize);
    }

    public async Task<PipelineAuditDashboardDto> GetDashboardSummaryAsync(
        Guid? tenantId, CancellationToken ct = default)
    {
        var effectiveTenantId = ResolveQueryTenantId(tenantId);

        // Deviation counts
        var deviationQuery = _dbContext.TranslationDeviations.AsQueryable();
        if (effectiveTenantId.HasValue)
            deviationQuery = deviationQuery.Where(d => d.TenantId == effectiveTenantId.Value);

        var deviationCounts = await deviationQuery
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int openDev = deviationCounts.FirstOrDefault(c => c.Status == DeviationStatus.Open)?.Count ?? 0;
        int inProgressDev = deviationCounts.FirstOrDefault(c => c.Status == DeviationStatus.InProgress)?.Count ?? 0;
        int closedDev = deviationCounts.FirstOrDefault(c => c.Status == DeviationStatus.Closed)?.Count ?? 0;

        // Change record count (system-wide)
        var changeRecordCount = await _dbContext.PipelineChangeRecords
            .IgnoreQueryFilters()
            .CountAsync(cr => !cr.IsDeleted, ct);

        // Locked terms count (tenant-scoped or global)
        var termQuery = _dbContext.SafetyGlossaryTerms.AsQueryable();
        if (effectiveTenantId.HasValue)
        {
            // Count terms in tenant's glossaries (override glossaries)
            var tenantGlossaryIds = await _dbContext.SafetyGlossaries
                .Where(g => g.TenantId == effectiveTenantId)
                .Select(g => g.Id)
                .ToListAsync(ct);
            termQuery = termQuery.Where(t => tenantGlossaryIds.Contains(t.GlossaryId));
        }
        var lockedTerms = await termQuery.CountAsync(ct);

        // Module outcomes count (completed runs)
        var runsQuery = _dbContext.TranslationValidationRuns
            .Where(r => r.Status == ValidationRunStatus.Completed);
        if (effectiveTenantId.HasValue)
            runsQuery = runsQuery.Where(r => r.TenantId == effectiveTenantId.Value);
        var moduleOutcomes = await runsQuery.CountAsync(ct);

        // Active pipeline version
        var activePipeline = await _dbContext.PipelineVersions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.IsActive && !v.IsDeleted, ct);

        // Most recent change record
        var latestCr = await _dbContext.PipelineChangeRecords
            .IgnoreQueryFilters()
            .Where(cr => !cr.IsDeleted)
            .Include(cr => cr.PipelineVersion)
            .OrderByDescending(cr => cr.DeployedAt)
            .FirstOrDefaultAsync(ct);

        // Top 3 open deviations
        var topOpenQuery = _dbContext.TranslationDeviations
            .Where(d => d.Status == DeviationStatus.Open);
        if (effectiveTenantId.HasValue)
            topOpenQuery = topOpenQuery.Where(d => d.TenantId == effectiveTenantId.Value);
        var topOpen = await topOpenQuery
            .OrderBy(d => d.DetectedAt)
            .Take(3)
            .ToListAsync(ct);

        return new PipelineAuditDashboardDto
        {
            OpenDeviations = openDev,
            InProgressDeviations = inProgressDev,
            ClosedDeviations = closedDev,
            ChangeRecords = changeRecordCount,
            LockedTerms = lockedTerms,
            ModuleOutcomes = moduleOutcomes,
            ActivePipelineVersion = activePipeline?.Version ?? "—",
            ActivePipelineHash = activePipeline?.Hash ?? "—",
            MostRecentChangeRecord = latestCr == null ? null : MapChangeRecordToDto(latestCr),
            TopOpenDeviations = topOpen.Select(MapDeviationToDto).ToList()
        };
    }

    public async Task<PipelineVersion?> GetActivePipelineVersionAsync(CancellationToken ct = default)
    {
        return await _dbContext.PipelineVersions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.IsActive && !v.IsDeleted, ct);
    }

    public async Task<PaginatedList<PipelineChangeRecordDto>> GetChangeRecordsAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _dbContext.PipelineChangeRecords
            .IgnoreQueryFilters()
            .Where(cr => !cr.IsDeleted)
            .Include(cr => cr.PipelineVersion)
            .OrderByDescending(cr => cr.DeployedAt)
            .Select(cr => MapChangeRecordToDto(cr));

        return await PaginatedList<PipelineChangeRecordDto>.CreateAsync(query, page, pageSize);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private Guid? ResolveQueryTenantId(Guid? requestedTenantId)
    {
        if (_currentUser.IsSuperUser && requestedTenantId.HasValue)
            return requestedTenantId;

        if (_currentUser.IsSuperUser)
            return null; // SuperUser with no filter sees all

        return _currentUser.TenantId;
    }

    private static PipelineChangeRecordDto MapChangeRecordToDto(PipelineChangeRecord cr) =>
        new()
        {
            Id = cr.Id,
            ChangeId = cr.ChangeId,
            Component = cr.Component,
            ChangeFrom = cr.ChangeFrom,
            ChangeTo = cr.ChangeTo,
            Justification = cr.Justification,
            ImpactAssessment = cr.ImpactAssessment,
            PriorModulesAction = cr.PriorModulesAction,
            Approver = cr.Approver,
            DeployedAt = cr.DeployedAt,
            PipelineVersionId = cr.PipelineVersionId,
            PipelineVersionHash = cr.PipelineVersion?.Hash,
            PipelineVersionLabel = cr.PipelineVersion?.Version,
            PreviousPipelineVersionId = cr.PreviousPipelineVersionId,
            CreatedAt = cr.CreatedAt
        };

    private static TranslationDeviationDto MapDeviationToDto(TranslationDeviation d) =>
        new()
        {
            Id = d.Id,
            DeviationId = d.DeviationId,
            DetectedAt = d.DetectedAt,
            DetectedBy = d.DetectedBy,
            ValidationRunId = d.ValidationRunId,
            ValidationResultId = d.ValidationResultId,
            ModuleRef = d.ModuleRef,
            LessonRef = d.LessonRef,
            LanguagePair = d.LanguagePair,
            SourceExcerpt = d.SourceExcerpt,
            TargetExcerpt = d.TargetExcerpt,
            Nature = d.Nature,
            RootCauseCategory = d.RootCauseCategory,
            RootCauseDetail = d.RootCauseDetail,
            CorrectiveAction = d.CorrectiveAction,
            PreventiveAction = d.PreventiveAction,
            Approver = d.Approver,
            Status = d.Status,
            ClosedBy = d.ClosedBy,
            ClosedAt = d.ClosedAt,
            PipelineVersionAtTime = d.PipelineVersionAtTime,
            CreatedAt = d.CreatedAt
        };
}
