using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Ingestion;

/// <summary>
/// Orchestrates AI-powered ingestion of regulatory requirements from document URLs.
/// Manages the fetch → extract → draft → review → approve pipeline.
/// </summary>
public class RequirementIngestionService : IRequirementIngestionService
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ILogger<RequirementIngestionService> _logger;

    public RequirementIngestionService(
        IToolboxTalksDbContext dbContext,
        ILogger<RequirementIngestionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IngestionSessionDto> StartIngestionAsync(
        Guid regulatoryDocumentId,
        string sourceUrl,
        CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.RegulatoryDocuments
            .FirstOrDefaultAsync(d => d.Id == regulatoryDocumentId, cancellationToken)
            ?? throw new InvalidOperationException($"Regulatory document {regulatoryDocumentId} not found");

        // Update SourceUrl if provided
        if (!string.IsNullOrWhiteSpace(sourceUrl))
        {
            document.SourceUrl = sourceUrl;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(document.SourceUrl))
            throw new InvalidOperationException("Document has no SourceUrl configured. Provide a URL to ingest from.");

        // Enqueue Hangfire background job
        BackgroundJob.Enqueue<RequirementIngestionJob>(
            job => job.ExecuteAsync(regulatoryDocumentId, CancellationToken.None));

        _logger.LogInformation("Enqueued ingestion job for document {DocumentId}", regulatoryDocumentId);

        return await BuildIngestionSessionDto(document, "Queued", cancellationToken);
    }

    public async Task<IngestionSessionDto> GetIngestionStatusAsync(
        Guid regulatoryDocumentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.RegulatoryDocuments
            .FirstOrDefaultAsync(d => d.Id == regulatoryDocumentId, cancellationToken)
            ?? throw new InvalidOperationException($"Regulatory document {regulatoryDocumentId} not found");

        // Determine status from LastIngestedAt and draft counts
        var status = document.LastIngestedAt.HasValue ? "Completed" : "Idle";

        return await BuildIngestionSessionDto(document, status, cancellationToken);
    }

    public async Task<List<DraftRequirementDto>> GetDraftRequirementsAsync(
        Guid regulatoryDocumentId,
        CancellationToken cancellationToken = default)
    {
        var profileIds = await _dbContext.RegulatoryProfiles
            .Where(p => p.RegulatoryDocumentId == regulatoryDocumentId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var requirements = await _dbContext.RegulatoryRequirements
            .Include(r => r.RegulatoryProfile)
                .ThenInclude(p => p.Sector)
            .Where(r => profileIds.Contains(r.RegulatoryProfileId)
                && r.IngestionStatus == RequirementIngestionStatus.Draft)
            .OrderBy(r => r.RegulatoryProfile.SectorKey)
            .ThenBy(r => r.DisplayOrder)
            .ToListAsync(cancellationToken);

        return requirements.Select(r => new DraftRequirementDto
        {
            Id = r.Id,
            Title = r.Title,
            Description = r.Description,
            Section = r.Section,
            SectionLabel = r.SectionLabel,
            Principle = r.Principle,
            PrincipleLabel = r.PrincipleLabel,
            Priority = r.Priority,
            DisplayOrder = r.DisplayOrder,
            IngestionSource = r.IngestionSource.ToString(),
            IngestionNotes = r.IngestionNotes,
            ProfileSectorKey = r.RegulatoryProfile.SectorKey,
            ProfileSectorName = r.RegulatoryProfile.Sector.Name,
        }).ToList();
    }

    public async Task<RegulatoryRequirementDto> ApproveRequirementAsync(
        Guid requirementId,
        ApproveRequirementRequest request,
        CancellationToken cancellationToken = default)
    {
        var requirement = await _dbContext.RegulatoryRequirements
            .Include(r => r.RegulatoryProfile)
                .ThenInclude(p => p.Sector)
            .FirstOrDefaultAsync(r => r.Id == requirementId, cancellationToken)
            ?? throw new InvalidOperationException($"Requirement {requirementId} not found");

        if (requirement.IngestionStatus != RequirementIngestionStatus.Draft)
            throw new InvalidOperationException("Only draft requirements can be approved");

        // Apply edits from request
        requirement.Title = request.Title;
        requirement.Description = request.Description;
        requirement.Section = request.Section;
        requirement.SectionLabel = request.SectionLabel;
        requirement.Principle = request.Principle;
        requirement.PrincipleLabel = request.PrincipleLabel;
        requirement.Priority = request.Priority;
        requirement.DisplayOrder = request.DisplayOrder;
        requirement.IngestionStatus = RequirementIngestionStatus.Approved;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Approved requirement {RequirementId}: {Title}", requirementId, requirement.Title);

        return MapToDto(requirement);
    }

    public async Task RejectRequirementAsync(
        Guid requirementId,
        string notes,
        CancellationToken cancellationToken = default)
    {
        var requirement = await _dbContext.RegulatoryRequirements
            .FirstOrDefaultAsync(r => r.Id == requirementId, cancellationToken)
            ?? throw new InvalidOperationException($"Requirement {requirementId} not found");

        if (requirement.IngestionStatus != RequirementIngestionStatus.Draft)
            throw new InvalidOperationException("Only draft requirements can be rejected");

        requirement.IngestionStatus = RequirementIngestionStatus.Rejected;
        requirement.IngestionNotes = notes;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Rejected requirement {RequirementId}: {Notes}", requirementId, notes);
    }

    public async Task<RegulatoryRequirementDto> UpdateDraftRequirementAsync(
        Guid requirementId,
        UpdateDraftRequirementRequest request,
        CancellationToken cancellationToken = default)
    {
        var requirement = await _dbContext.RegulatoryRequirements
            .Include(r => r.RegulatoryProfile)
                .ThenInclude(p => p.Sector)
            .FirstOrDefaultAsync(r => r.Id == requirementId, cancellationToken)
            ?? throw new InvalidOperationException($"Requirement {requirementId} not found");

        if (requirement.IngestionStatus != RequirementIngestionStatus.Draft)
            throw new InvalidOperationException("Only draft requirements can be updated");

        requirement.Title = request.Title;
        requirement.Description = request.Description;
        requirement.Section = request.Section;
        requirement.SectionLabel = request.SectionLabel;
        requirement.Principle = request.Principle;
        requirement.PrincipleLabel = request.PrincipleLabel;
        requirement.Priority = request.Priority;
        requirement.DisplayOrder = request.DisplayOrder;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(requirement);
    }

    public async Task<int> ApproveAllDraftRequirementsAsync(
        Guid regulatoryDocumentId,
        CancellationToken cancellationToken = default)
    {
        var profileIds = await _dbContext.RegulatoryProfiles
            .Where(p => p.RegulatoryDocumentId == regulatoryDocumentId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var drafts = await _dbContext.RegulatoryRequirements
            .Where(r => profileIds.Contains(r.RegulatoryProfileId)
                && r.IngestionStatus == RequirementIngestionStatus.Draft)
            .ToListAsync(cancellationToken);

        foreach (var draft in drafts)
        {
            draft.IngestionStatus = RequirementIngestionStatus.Approved;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Approved {Count} draft requirements for document {DocumentId}",
            drafts.Count, regulatoryDocumentId);

        return drafts.Count;
    }

    public async Task<List<RegulatoryDocumentListDto>> GetDocumentsAsync(
        CancellationToken cancellationToken = default)
    {
        var documents = await _dbContext.RegulatoryDocuments
            .Include(d => d.RegulatoryBody)
            .Include(d => d.Profiles)
                .ThenInclude(p => p.Sector)
            .OrderBy(d => d.RegulatoryBody.Name)
            .ThenBy(d => d.Title)
            .ToListAsync(cancellationToken);

        var result = new List<RegulatoryDocumentListDto>();

        foreach (var doc in documents)
        {
            var profileIds = doc.Profiles.Select(p => p.Id).ToList();

            var counts = await _dbContext.RegulatoryRequirements
                .IgnoreQueryFilters()
                .Where(r => !r.IsDeleted && profileIds.Contains(r.RegulatoryProfileId))
                .GroupBy(r => r.IngestionStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            result.Add(new RegulatoryDocumentListDto
            {
                Id = doc.Id,
                RegulatoryBodyName = doc.RegulatoryBody.Name,
                RegulatoryBodyCode = doc.RegulatoryBody.Code,
                Title = doc.Title,
                Version = doc.Version,
                Source = doc.Source,
                SourceUrl = doc.SourceUrl,
                EffectiveDate = doc.EffectiveDate,
                IsActive = doc.IsActive,
                LastIngestedAt = doc.LastIngestedAt,
                SectorKeys = doc.Profiles.Select(p => p.SectorKey).ToList(),
                DraftCount = counts.FirstOrDefault(c => c.Status == RequirementIngestionStatus.Draft)?.Count ?? 0,
                ApprovedCount = counts.FirstOrDefault(c => c.Status == RequirementIngestionStatus.Approved)?.Count ?? 0,
                RejectedCount = counts.FirstOrDefault(c => c.Status == RequirementIngestionStatus.Rejected)?.Count ?? 0,
            });
        }

        return result;
    }

    private async Task<IngestionSessionDto> BuildIngestionSessionDto(
        Domain.Entities.RegulatoryDocument document,
        string status,
        CancellationToken cancellationToken)
    {
        var profileIds = await _dbContext.RegulatoryProfiles
            .Where(p => p.RegulatoryDocumentId == document.Id)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var counts = await _dbContext.RegulatoryRequirements
            .IgnoreQueryFilters()
            .Where(r => !r.IsDeleted && profileIds.Contains(r.RegulatoryProfileId))
            .GroupBy(r => r.IngestionStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return new IngestionSessionDto
        {
            RegulatoryDocumentId = document.Id,
            DocumentTitle = document.Title,
            SourceUrl = document.SourceUrl,
            Status = status,
            LastIngestedAt = document.LastIngestedAt,
            DraftCount = counts.FirstOrDefault(c => c.Status == RequirementIngestionStatus.Draft)?.Count ?? 0,
            ApprovedCount = counts.FirstOrDefault(c => c.Status == RequirementIngestionStatus.Approved)?.Count ?? 0,
            RejectedCount = counts.FirstOrDefault(c => c.Status == RequirementIngestionStatus.Rejected)?.Count ?? 0,
        };
    }

    private static RegulatoryRequirementDto MapToDto(Domain.Entities.RegulatoryRequirement r)
    {
        return new RegulatoryRequirementDto
        {
            Id = r.Id,
            Title = r.Title,
            Description = r.Description,
            Section = r.Section,
            SectionLabel = r.SectionLabel,
            Principle = r.Principle,
            PrincipleLabel = r.PrincipleLabel,
            Priority = r.Priority,
            DisplayOrder = r.DisplayOrder,
            IngestionSource = r.IngestionSource.ToString(),
            IngestionStatus = r.IngestionStatus.ToString(),
            IngestionNotes = r.IngestionNotes,
            ProfileSectorKey = r.RegulatoryProfile.SectorKey,
            ProfileSectorName = r.RegulatoryProfile.Sector.Name,
        };
    }
}
