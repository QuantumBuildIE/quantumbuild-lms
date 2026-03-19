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

    public async Task<ComplianceChecklistDto> GetComplianceChecklistAsync(
        string sectorKey, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;

        // Validate tenant has this sector
        var hasSector = await _dbContext.TenantSectors
            .AnyAsync(ts => ts.TenantId == tenantId && ts.Sector.Key == sectorKey && !ts.IsDeleted, cancellationToken);
        if (!hasSector)
            throw new UnauthorizedAccessException($"Tenant does not have sector '{sectorKey}' configured.");

        // Load approved, active requirements for this sector via RegulatoryProfile
        var requirements = await _dbContext.RegulatoryRequirements
            .IgnoreQueryFilters()
            .Where(r => !r.IsDeleted
                && r.IsActive
                && r.IngestionStatus == RequirementIngestionStatus.Approved
                && r.RegulatoryProfile.SectorKey == sectorKey)
            .Include(r => r.RegulatoryProfile)
                .ThenInclude(p => p.RegulatoryDocument)
                    .ThenInclude(d => d.RegulatoryBody)
            .Include(r => r.RegulatoryProfile)
                .ThenInclude(p => p.Sector)
            .OrderBy(r => r.Principle)
            .ThenBy(r => r.DisplayOrder)
            .ToListAsync(cancellationToken);

        if (requirements.Count == 0)
        {
            // Get sector name for empty response
            var sector = await _dbContext.Sectors
                .FirstOrDefaultAsync(s => s.Key == sectorKey && !s.IsDeleted, cancellationToken);

            return new ComplianceChecklistDto(
                SectorKey: sectorKey,
                SectorName: sector?.Name ?? sectorKey,
                RegulatoryBody: "",
                ScoreLabel: "",
                TotalRequirements: 0,
                CoveredCount: 0,
                PendingCount: 0,
                GapCount: 0,
                CoveragePercentage: 0,
                PrincipleGroups: [],
                LastUpdated: DateTimeOffset.UtcNow
            );
        }

        var requirementIds = requirements.Select(r => r.Id).ToList();

        // Load tenant mappings for these requirements
        var mappings = await _dbContext.RegulatoryRequirementMappings
            .Where(m => m.TenantId == tenantId && requirementIds.Contains(m.RegulatoryRequirementId))
            .Include(m => m.ToolboxTalk)
            .Include(m => m.Course)
            .ToListAsync(cancellationToken);

        // Load validation runs for mapped content to determine "approved" validation
        var talkIds = mappings.Where(m => m.ToolboxTalkId.HasValue).Select(m => m.ToolboxTalkId!.Value).Distinct().ToList();
        var courseIds = mappings.Where(m => m.CourseId.HasValue).Select(m => m.CourseId!.Value).Distinct().ToList();

        // Get the most recent completed validation run per talk/course with Pass or Review outcome
        var talkValidationRuns = talkIds.Count > 0
            ? await _dbContext.TranslationValidationRuns
                .Where(v => v.TenantId == tenantId
                    && v.ToolboxTalkId.HasValue
                    && talkIds.Contains(v.ToolboxTalkId.Value)
                    && v.Status == ValidationRunStatus.Completed
                    && (v.OverallOutcome == ValidationOutcome.Pass || v.OverallOutcome == ValidationOutcome.Review))
                .GroupBy(v => v.ToolboxTalkId!.Value)
                .Select(g => g.OrderByDescending(v => v.CompletedAt).First())
                .ToListAsync(cancellationToken)
            : [];

        var courseValidationRuns = courseIds.Count > 0
            ? await _dbContext.TranslationValidationRuns
                .Where(v => v.TenantId == tenantId
                    && v.CourseId.HasValue
                    && courseIds.Contains(v.CourseId.Value)
                    && v.Status == ValidationRunStatus.Completed
                    && (v.OverallOutcome == ValidationOutcome.Pass || v.OverallOutcome == ValidationOutcome.Review))
                .GroupBy(v => v.CourseId!.Value)
                .Select(g => g.OrderByDescending(v => v.CompletedAt).First())
                .ToListAsync(cancellationToken)
            : [];

        var talkValidationLookup = talkValidationRuns.ToDictionary(v => v.ToolboxTalkId!.Value);
        var courseValidationLookup = courseValidationRuns.ToDictionary(v => v.CourseId!.Value);

        // Build requirement DTOs with coverage status
        var mappingsByRequirement = mappings
            .GroupBy(m => m.RegulatoryRequirementId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var lastUpdated = mappings.Count > 0
            ? mappings.Max(m => m.ReviewedAt ?? m.UpdatedAt ?? m.CreatedAt)
            : DateTimeOffset.UtcNow;

        var requirementDtos = new List<ComplianceRequirementDto>();
        foreach (var req in requirements)
        {
            var reqMappings = mappingsByRequirement.GetValueOrDefault(req.Id, []);

            var mappingDetails = reqMappings
                .Where(m => m.MappingStatus != RequirementMappingStatus.Rejected)
                .Select(m =>
                {
                    var contentId = m.ToolboxTalkId ?? m.CourseId ?? Guid.Empty;
                    var contentTitle = m.ToolboxTalkId.HasValue
                        ? m.ToolboxTalk?.Title ?? "Unknown Talk"
                        : m.Course?.Title ?? "Unknown Course";
                    var contentType = m.ToolboxTalkId.HasValue ? "Talk" : "Course";

                    Domain.Entities.TranslationValidationRun? validationRun = null;
                    if (m.ToolboxTalkId.HasValue)
                        talkValidationLookup.TryGetValue(m.ToolboxTalkId.Value, out validationRun);
                    else if (m.CourseId.HasValue)
                        courseValidationLookup.TryGetValue(m.CourseId.Value, out validationRun);

                    return new MappingDetailDto(
                        MappingId: m.Id,
                        ContentId: contentId,
                        ContentTitle: contentTitle,
                        ContentType: contentType,
                        MappingStatus: m.MappingStatus.ToString(),
                        ConfidenceScore: m.ConfidenceScore,
                        ValidationScore: validationRun?.OverallScore,
                        ValidationOutcome: validationRun?.OverallOutcome.ToString(),
                        ValidationDate: validationRun?.CompletedAt.HasValue == true
                            ? new DateTimeOffset(validationRun.CompletedAt.Value, TimeSpan.Zero)
                            : null
                    );
                })
                .ToList();

            // Determine coverage status
            var hasConfirmedWithValidation = mappingDetails.Any(md =>
                md.MappingStatus == RequirementMappingStatus.Confirmed.ToString()
                && md.ValidationOutcome != null);
            var hasSuggested = mappingDetails.Any(md =>
                md.MappingStatus == RequirementMappingStatus.Suggested.ToString());
            var hasConfirmedWithoutValidation = mappingDetails.Any(md =>
                md.MappingStatus == RequirementMappingStatus.Confirmed.ToString()
                && md.ValidationOutcome == null);

            string coverageStatus;
            if (hasConfirmedWithValidation)
                coverageStatus = "Covered";
            else if (hasSuggested || hasConfirmedWithoutValidation)
                coverageStatus = "Pending";
            else
                coverageStatus = "Gap";

            requirementDtos.Add(new ComplianceRequirementDto(
                Id: req.Id,
                Title: req.Title,
                Description: req.Description,
                Section: req.Section,
                SectionLabel: req.SectionLabel,
                Principle: req.Principle,
                PrincipleLabel: req.PrincipleLabel,
                Priority: req.Priority,
                DisplayOrder: req.DisplayOrder,
                CoverageStatus: coverageStatus,
                Mappings: mappingDetails
            ));
        }

        // Group by principle
        var principleGroups = requirementDtos
            .GroupBy(r => new { r.Principle, r.PrincipleLabel })
            .Select(g => new CompliancePrincipleGroupDto(
                Principle: g.Key.Principle ?? "",
                PrincipleLabel: g.Key.PrincipleLabel ?? "",
                TotalRequirements: g.Count(),
                CoveredCount: g.Count(r => r.CoverageStatus == "Covered"),
                PendingCount: g.Count(r => r.CoverageStatus == "Pending"),
                GapCount: g.Count(r => r.CoverageStatus == "Gap"),
                Requirements: g.ToList()
            ))
            .ToList();

        var totalReqs = requirementDtos.Count;
        var coveredCount = requirementDtos.Count(r => r.CoverageStatus == "Covered");
        var pendingCount = requirementDtos.Count(r => r.CoverageStatus == "Pending");
        var gapCount = requirementDtos.Count(r => r.CoverageStatus == "Gap");

        // Get regulatory body + score label from the first profile
        var firstProfile = requirements[0].RegulatoryProfile;
        var regulatoryBody = firstProfile.RegulatoryDocument.RegulatoryBody.Name;
        var scoreLabel = firstProfile.ScoreLabel;
        var sectorName = firstProfile.Sector?.Name ?? sectorKey;

        return new ComplianceChecklistDto(
            SectorKey: sectorKey,
            SectorName: sectorName,
            RegulatoryBody: regulatoryBody,
            ScoreLabel: scoreLabel,
            TotalRequirements: totalReqs,
            CoveredCount: coveredCount,
            PendingCount: pendingCount,
            GapCount: gapCount,
            CoveragePercentage: totalReqs > 0 ? (int)Math.Round(100.0 * coveredCount / totalReqs) : 0,
            PrincipleGroups: principleGroups,
            LastUpdated: lastUpdated
        );
    }

    public async Task<PendingMappingDto> AddManualMappingAsync(
        AddManualMappingRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;

        // Validate: exactly one of ToolboxTalkId or CourseId
        if ((!request.ToolboxTalkId.HasValue && !request.CourseId.HasValue)
            || (request.ToolboxTalkId.HasValue && request.CourseId.HasValue))
        {
            throw new InvalidOperationException("Exactly one of ToolboxTalkId or CourseId must be provided.");
        }

        // Check for existing soft-deleted mapping (restore-on-reassign pattern)
        var existing = await _dbContext.RegulatoryRequirementMappings
            .IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId
                && m.RegulatoryRequirementId == request.RegulatoryRequirementId
                && m.ToolboxTalkId == request.ToolboxTalkId
                && m.CourseId == request.CourseId)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing != null)
        {
            // Restore soft-deleted or update existing
            existing.IsDeleted = false;
            existing.MappingStatus = RequirementMappingStatus.Confirmed;
            existing.ConfidenceScore = null;
            existing.AiReasoning = null;
            existing.ReviewedBy = _currentUser.UserName;
            existing.ReviewedAt = DateTimeOffset.UtcNow;
            existing.ReviewNotes = null;
        }
        else
        {
            existing = new Domain.Entities.RegulatoryRequirementMapping
            {
                TenantId = tenantId,
                RegulatoryRequirementId = request.RegulatoryRequirementId,
                ToolboxTalkId = request.ToolboxTalkId,
                CourseId = request.CourseId,
                MappingStatus = RequirementMappingStatus.Confirmed,
                ConfidenceScore = null,
                AiReasoning = null,
                ReviewedBy = _currentUser.UserName,
                ReviewedAt = DateTimeOffset.UtcNow,
            };
            _dbContext.RegulatoryRequirementMappings.Add(existing);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Reload with navigation properties for DTO
        var mapping = await _dbContext.RegulatoryRequirementMappings
            .Where(m => m.Id == existing.Id)
            .Include(m => m.RegulatoryRequirement)
            .Include(m => m.ToolboxTalk)
            .Include(m => m.Course)
            .FirstAsync(cancellationToken);

        return ToDto(mapping);
    }

    public async Task<List<ContentOptionDto>> GetContentOptionsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;

        var talks = await _dbContext.ToolboxTalks
            .Where(t => t.TenantId == tenantId
                && t.Status == ToolboxTalkStatus.Published)
            .OrderBy(t => t.Title)
            .Select(t => new ContentOptionDto(t.Id, t.Title, "Talk"))
            .ToListAsync(cancellationToken);

        var courses = await _dbContext.ToolboxTalkCourses
            .Where(c => c.TenantId == tenantId
                && c.IsActive)
            .OrderBy(c => c.Title)
            .Select(c => new ContentOptionDto(c.Id, c.Title, "Course"))
            .ToListAsync(cancellationToken);

        // Courses first, then talks
        courses.AddRange(talks);
        return courses;
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
