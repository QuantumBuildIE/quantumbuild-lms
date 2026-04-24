using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

public class TranslationDeviationService : ITranslationDeviationService
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IPipelineVersionService _pipelineVersionService;
    private readonly ILogger<TranslationDeviationService> _logger;

    public TranslationDeviationService(
        IToolboxTalksDbContext dbContext,
        ICurrentUserService currentUser,
        IPipelineVersionService pipelineVersionService,
        ILogger<TranslationDeviationService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _pipelineVersionService = pipelineVersionService;
        _logger = logger;
    }

    public async Task<TranslationDeviationDto> CreateAsync(
        CreateDeviationRequest request, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;

        var deviationId = await GenerateDeviationIdAsync(tenantId, ct);

        var activePipeline = await _pipelineVersionService.GetActiveAsync(ct);

        var deviation = new TranslationDeviation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DeviationId = deviationId,
            DetectedAt = DateTimeOffset.UtcNow,
            DetectedBy = request.DetectedBy,
            ValidationRunId = request.ValidationRunId,
            ValidationResultId = request.ValidationResultId,
            ModuleRef = request.ModuleRef,
            LessonRef = request.LessonRef,
            LanguagePair = request.LanguagePair,
            SourceExcerpt = request.SourceExcerpt,
            TargetExcerpt = request.TargetExcerpt,
            Nature = request.Nature,
            RootCauseCategory = request.RootCauseCategory,
            RootCauseDetail = request.RootCauseDetail,
            CorrectiveAction = request.CorrectiveAction,
            PreventiveAction = request.PreventiveAction,
            Approver = request.Approver,
            Status = DeviationStatus.Open,
            PipelineVersionAtTime = activePipeline?.Hash
        };

        _dbContext.TranslationDeviations.Add(deviation);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Deviation {DeviationId} created for tenant {TenantId}", deviationId, tenantId);

        return MapToDto(deviation);
    }

    public async Task<TranslationDeviationDto> UpdateStatusAsync(
        Guid id, DeviationStatus status, string? closedBy, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;

        var deviation = await _dbContext.TranslationDeviations
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Deviation {id} not found");

        deviation.Status = status;

        if (status == DeviationStatus.Closed)
        {
            deviation.ClosedBy = closedBy ?? _currentUser.UserName;
            deviation.ClosedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            deviation.ClosedBy = null;
            deviation.ClosedAt = null;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Deviation {DeviationId} status updated to {Status}", deviation.DeviationId, status);

        return MapToDto(deviation);
    }

    public async Task<PaginatedList<TranslationDeviationDto>> GetPagedAsync(
        DeviationStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;

        var query = _dbContext.TranslationDeviations
            .Where(d => d.TenantId == tenantId);

        if (status.HasValue)
            query = query.Where(d => d.Status == status.Value);

        var ordered = query
            .OrderByDescending(d => d.DetectedAt)
            .Select(d => MapToDto(d));

        return await PaginatedList<TranslationDeviationDto>.CreateAsync(ordered, page, pageSize);
    }

    public async Task<TranslationDeviationDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;

        var deviation = await _dbContext.TranslationDeviations
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId, ct);

        return deviation == null ? null : MapToDto(deviation);
    }

    public async Task<DeviationSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;

        var counts = await _dbContext.TranslationDeviations
            .Where(d => d.TenantId == tenantId)
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int open = counts.FirstOrDefault(c => c.Status == DeviationStatus.Open)?.Count ?? 0;
        int inProgress = counts.FirstOrDefault(c => c.Status == DeviationStatus.InProgress)?.Count ?? 0;
        int closed = counts.FirstOrDefault(c => c.Status == DeviationStatus.Closed)?.Count ?? 0;

        return new DeviationSummaryDto
        {
            OpenCount = open,
            InProgressCount = inProgress,
            ClosedCount = closed,
            Total = open + inProgress + closed
        };
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private async Task<string> GenerateDeviationIdAsync(Guid tenantId, CancellationToken ct)
    {
        var existingIds = await _dbContext.TranslationDeviations
            .IgnoreQueryFilters()
            .Where(d => d.TenantId == tenantId && d.DeviationId.StartsWith("DEV-"))
            .Select(d => d.DeviationId)
            .ToListAsync(ct);

        int maxSuffix = 0;
        foreach (var id in existingIds)
        {
            var suffix = id[4..]; // "DEV-001" → "001"
            if (int.TryParse(suffix, out var num) && num > maxSuffix)
                maxSuffix = num;
        }

        return $"DEV-{maxSuffix + 1:D3}";
    }

    private static TranslationDeviationDto MapToDto(TranslationDeviation d) =>
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
