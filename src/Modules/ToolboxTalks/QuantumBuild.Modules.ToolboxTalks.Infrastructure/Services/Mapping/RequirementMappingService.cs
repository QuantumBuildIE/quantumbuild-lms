using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Mapping;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Mapping;

/// <summary>
/// Manages AI-suggested regulatory requirement mappings for tenant admins.
/// All queries are scoped to the current tenant via ICurrentUserService.
/// </summary>
public class RequirementMappingService : IRequirementMappingService
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<RequirementMappingService> _logger;

    public RequirementMappingService(
        IToolboxTalksDbContext dbContext,
        ICurrentUserService currentUser,
        ILogger<RequirementMappingService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<MappingSummaryDto> GetPendingMappingsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;

        var allMappings = await _dbContext.RegulatoryRequirementMappings
            .Where(m => m.TenantId == tenantId)
            .Include(m => m.RegulatoryRequirement)
            .Include(m => m.ToolboxTalk)
            .Include(m => m.Course)
            .ToListAsync(cancellationToken);

        var totalSuggested = allMappings.Count(m => m.MappingStatus == RequirementMappingStatus.Suggested);
        var totalConfirmed = allMappings.Count(m => m.MappingStatus == RequirementMappingStatus.Confirmed);
        var totalRejected = allMappings.Count(m => m.MappingStatus == RequirementMappingStatus.Rejected);

        var pendingReview = allMappings
            .Where(m => m.MappingStatus == RequirementMappingStatus.Suggested)
            .OrderByDescending(m => m.ConfidenceScore)
            .Select(m => new PendingMappingDto(
                Id: m.Id,
                RegulatoryRequirementId: m.RegulatoryRequirementId,
                RequirementTitle: m.RegulatoryRequirement.Title,
                RequirementDescription: m.RegulatoryRequirement.Description,
                RequirementSection: m.RegulatoryRequirement.Section,
                RequirementSectionLabel: m.RegulatoryRequirement.SectionLabel,
                RequirementPrinciple: m.RegulatoryRequirement.Principle,
                RequirementPrincipleLabel: m.RegulatoryRequirement.PrincipleLabel,
                RequirementPriority: m.RegulatoryRequirement.Priority,
                ConfidenceScore: m.ConfidenceScore,
                AiReasoning: m.AiReasoning,
                ReviewNotes: m.ReviewNotes,
                MappingStatus: m.MappingStatus.ToString(),
                ContentTitle: m.ToolboxTalkId.HasValue
                    ? m.ToolboxTalk?.Title ?? "Unknown Talk"
                    : m.Course?.Title ?? "Unknown Course",
                ContentType: m.ToolboxTalkId.HasValue ? "Talk" : "Course",
                ContentId: m.ToolboxTalkId ?? m.CourseId ?? Guid.Empty,
                CreatedAt: m.CreatedAt
            ))
            .ToList();

        return new MappingSummaryDto(totalSuggested, totalConfirmed, totalRejected, pendingReview);
    }

    public async Task<PendingMappingDto> ConfirmMappingAsync(Guid mappingId, CancellationToken cancellationToken = default)
    {
        var mapping = await _dbContext.RegulatoryRequirementMappings
            .Where(m => m.TenantId == _currentUser.TenantId && m.Id == mappingId)
            .Include(m => m.RegulatoryRequirement)
            .Include(m => m.ToolboxTalk)
            .Include(m => m.Course)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Mapping {mappingId} not found");

        mapping.MappingStatus = RequirementMappingStatus.Confirmed;
        mapping.ReviewedBy = _currentUser.UserName;
        mapping.ReviewedAt = DateTimeOffset.UtcNow;
        mapping.ReviewNotes = null;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(mapping);
    }

    public async Task RejectMappingAsync(Guid mappingId, string? notes, CancellationToken cancellationToken = default)
    {
        var mapping = await _dbContext.RegulatoryRequirementMappings
            .Where(m => m.TenantId == _currentUser.TenantId && m.Id == mappingId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Mapping {mappingId} not found");

        mapping.MappingStatus = RequirementMappingStatus.Rejected;
        mapping.ReviewedBy = _currentUser.UserName;
        mapping.ReviewedAt = DateTimeOffset.UtcNow;
        mapping.ReviewNotes = notes;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ConfirmAllSuggestedAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;

        var suggestedMappings = await _dbContext.RegulatoryRequirementMappings
            .Where(m => m.TenantId == tenantId && m.MappingStatus == RequirementMappingStatus.Suggested)
            .ToListAsync(cancellationToken);

        foreach (var mapping in suggestedMappings)
        {
            mapping.MappingStatus = RequirementMappingStatus.Confirmed;
            mapping.ReviewedBy = _currentUser.UserName;
            mapping.ReviewedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Confirmed {Count} suggested mappings for tenant {TenantId}",
            suggestedMappings.Count, tenantId);

        return suggestedMappings.Count;
    }

    public async Task<int> GetUnconfirmedCountAsync(
        Guid? toolboxTalkId, Guid? courseId, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;

        var query = _dbContext.RegulatoryRequirementMappings
            .Where(m => m.TenantId == tenantId && m.MappingStatus == RequirementMappingStatus.Suggested);

        if (toolboxTalkId.HasValue)
            query = query.Where(m => m.ToolboxTalkId == toolboxTalkId);
        else if (courseId.HasValue)
            query = query.Where(m => m.CourseId == courseId);
        else
            return 0;

        return await query.CountAsync(cancellationToken);
    }

    private static PendingMappingDto ToDto(Domain.Entities.RegulatoryRequirementMapping m) =>
        new(
            Id: m.Id,
            RegulatoryRequirementId: m.RegulatoryRequirementId,
            RequirementTitle: m.RegulatoryRequirement.Title,
            RequirementDescription: m.RegulatoryRequirement.Description,
            RequirementSection: m.RegulatoryRequirement.Section,
            RequirementSectionLabel: m.RegulatoryRequirement.SectionLabel,
            RequirementPrinciple: m.RegulatoryRequirement.Principle,
            RequirementPrincipleLabel: m.RegulatoryRequirement.PrincipleLabel,
            RequirementPriority: m.RegulatoryRequirement.Priority,
            ConfidenceScore: m.ConfidenceScore,
            AiReasoning: m.AiReasoning,
            ReviewNotes: m.ReviewNotes,
            MappingStatus: m.MappingStatus.ToString(),
            ContentTitle: m.ToolboxTalkId.HasValue
                ? m.ToolboxTalk?.Title ?? "Unknown Talk"
                : m.Course?.Title ?? "Unknown Course",
            ContentType: m.ToolboxTalkId.HasValue ? "Talk" : "Course",
            ContentId: m.ToolboxTalkId ?? m.CourseId ?? Guid.Empty,
            CreatedAt: m.CreatedAt
        );
}
