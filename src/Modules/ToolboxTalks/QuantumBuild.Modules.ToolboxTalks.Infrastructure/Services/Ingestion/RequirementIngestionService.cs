using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Frameworks;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Validation;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Application.Exceptions;
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
    private readonly IR2StorageService _storageService;
    private readonly IApplicableFrameworksService _applicableFrameworksService;
    private readonly ILogger<RequirementIngestionService> _logger;

    public RequirementIngestionService(
        IToolboxTalksDbContext dbContext,
        IR2StorageService storageService,
        IApplicableFrameworksService applicableFrameworksService,
        ILogger<RequirementIngestionService> logger)
    {
        _dbContext = dbContext;
        _storageService = storageService;
        _applicableFrameworksService = applicableFrameworksService;
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

        // Validate before enqueueing — synchronously, within the same request, well before
        // the job ever runs. Rejects the effective SourceUrl (whether freshly provided above
        // or previously persisted) so a bad URL can never reach the Hangfire job silently.
        if (!SourceUrlValidator.IsValid(document.SourceUrl, out var validationError))
        {
            _logger.LogWarning(
                "Rejected ingestion start for document {DocumentId}: {Error}",
                regulatoryDocumentId, validationError);
            throw new InvalidSourceUrlException(validationError!);
        }

        // Enqueue Hangfire background job
        BackgroundJob.Enqueue<RequirementIngestionJob>(
            job => job.ExecuteAsync(regulatoryDocumentId, CancellationToken.None));

        _logger.LogInformation("Enqueued ingestion job for document {DocumentId}", regulatoryDocumentId);

        return await BuildIngestionSessionDto(document, cancellationToken);
    }

    public async Task<IngestionSessionDto> GetIngestionStatusAsync(
        Guid regulatoryDocumentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.RegulatoryDocuments
            .FirstOrDefaultAsync(d => d.Id == regulatoryDocumentId, cancellationToken)
            ?? throw new InvalidOperationException($"Regulatory document {regulatoryDocumentId} not found");

        return await BuildIngestionSessionDto(document, cancellationToken);
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
            .IgnoreQueryFilters()
            .Include(r => r.RegulatoryProfile)
                .ThenInclude(p => p.Sector)
            .Where(r => !r.IsDeleted
                && profileIds.Contains(r.RegulatoryProfileId)
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

    public async Task<List<RegulatoryBrowseBodyDto>> GetBrowsableRequirementsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // Union of entitlements: Regulations apply via the tenant's assigned sectors,
        // Standards apply via an active TenantStandardSubscription (independent of sector).
        var entitlements = await _applicableFrameworksService.GetTenantEntitlementsAsync(tenantId, cancellationToken);

        if (entitlements.SectorKeys.Count == 0 && entitlements.SubscribedStandardBodyIds.Count == 0)
            return new List<RegulatoryBrowseBodyDto>();

        // Load approved requirements whose body is a Regulation matching a tenant sector,
        // or whose body is a Standard the tenant is subscribed to.
        var requirements = await _dbContext.RegulatoryRequirements
            .IgnoreQueryFilters()
            .Include(r => r.RegulatoryProfile)
                .ThenInclude(p => p.Sector)
            .Include(r => r.RegulatoryProfile)
                .ThenInclude(p => p.RegulatoryDocument)
                    .ThenInclude(d => d.RegulatoryBody)
            .Where(r => !r.IsDeleted
                && r.IngestionStatus == RequirementIngestionStatus.Approved
                && (
                    (r.RegulatoryProfile.RegulatoryDocument.RegulatoryBody.Kind == RegulatoryBodyKind.Regulation
                        && entitlements.SectorKeys.Contains(r.RegulatoryProfile.SectorKey))
                    || (r.RegulatoryProfile.RegulatoryDocument.RegulatoryBody.Kind == RegulatoryBodyKind.Standard
                        && entitlements.SubscribedStandardBodyIds.Contains(r.RegulatoryProfile.RegulatoryDocument.RegulatoryBodyId))
                ))
            .OrderBy(r => r.RegulatoryProfile.RegulatoryDocument.RegulatoryBody.Name)
            .ThenBy(r => r.RegulatoryProfile.RegulatoryDocument.Title)
            .ThenBy(r => r.Principle)
            .ThenBy(r => r.DisplayOrder)
            .ToListAsync(cancellationToken);

        // Group: Body → Document → PrincipleGroup
        var result = requirements
            .GroupBy(r => r.RegulatoryProfile.RegulatoryDocument.RegulatoryBody)
            .Select(bodyGroup => new RegulatoryBrowseBodyDto
            {
                Id = bodyGroup.Key.Id,
                Name = bodyGroup.Key.Name,
                Code = bodyGroup.Key.Code,
                Country = bodyGroup.Key.Country,
                Kind = bodyGroup.Key.Kind.ToString(),
                Documents = bodyGroup
                    .GroupBy(r => r.RegulatoryProfile.RegulatoryDocument)
                    .Select(docGroup => new RegulatoryBrowseDocumentDto
                    {
                        Id = docGroup.Key.Id,
                        Title = docGroup.Key.Title,
                        Version = docGroup.Key.Version,
                        SectorKeys = docGroup
                            .Select(r => r.RegulatoryProfile.SectorKey)
                            .Distinct()
                            .ToList(),
                        PrincipleGroups = docGroup
                            .GroupBy(r => new { r.Principle, r.PrincipleLabel })
                            .Select(pg => new RegulatoryBrowsePrincipleGroupDto
                            {
                                Principle = pg.Key.Principle,
                                PrincipleLabel = pg.Key.PrincipleLabel,
                                Requirements = pg.Select(r => new RegulatoryBrowseRequirementDto
                                {
                                    Id = r.Id,
                                    Title = r.Title,
                                    Description = r.Description,
                                    Priority = r.Priority,
                                    Section = r.Section,
                                    SectionLabel = r.SectionLabel,
                                    SectorKey = r.RegulatoryProfile.SectorKey,
                                    SectorName = r.RegulatoryProfile.Sector.Name,
                                }).ToList(),
                            })
                            .ToList(),
                    })
                    .ToList(),
            })
            .ToList();

        return result;
    }

    public async Task<RegulatoryDocumentUploadResponseDto?> UploadSourceDocumentAsync(
        Guid regulatoryDocumentId,
        Stream fileContent,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.RegulatoryDocuments
            .FirstOrDefaultAsync(d => d.Id == regulatoryDocumentId, cancellationToken);

        if (document == null)
            return null;

        var result = await _storageService.UploadRegulatoryDocumentAsync(
            regulatoryDocumentId, fileContent, cancellationToken);

        if (!result.Success)
            throw new InvalidOperationException(result.ErrorMessage ?? "Upload failed");

        document.SourceUrl = result.PublicUrl;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Uploaded source document for regulatory document {DocumentId}: {Url}",
            regulatoryDocumentId, result.PublicUrl);

        return new RegulatoryDocumentUploadResponseDto
        {
            SourceUrl = result.PublicUrl!,
            FileName = originalFileName,
            FileSizeBytes = result.FileSizeBytes!.Value
        };
    }

    public async Task<List<RegulatoryBodyDto>> GetRegulatoryBodiesAsync(
        RegulatoryBodyKind? kind = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.RegulatoryBodies
            .Include(b => b.Sector)
            .AsQueryable();

        if (kind is not null)
            query = query.Where(b => b.Kind == kind.Value);

        return await query
            .OrderBy(b => b.Name)
            .Select(b => new RegulatoryBodyDto
            {
                Id = b.Id,
                Name = b.Name,
                Code = b.Code,
                Country = b.Country,
                Kind = b.Kind.ToString(),
                SectorId = b.SectorId,
                SectorName = b.Sector != null ? b.Sector.Name : null,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<RegulatoryBodyDto> CreateRegulatoryBodyAsync(
        CreateRegulatoryBodyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Name is required.");

        if (request.Name.Trim().Length > 100)
            throw new InvalidOperationException("Name must be 100 characters or fewer.");

        if (string.IsNullOrWhiteSpace(request.Code))
            throw new InvalidOperationException("Code is required.");

        if (request.Code.Trim().Length > 20)
            throw new InvalidOperationException("Code must be 20 characters or fewer.");

        if (string.IsNullOrWhiteSpace(request.Country))
            throw new InvalidOperationException("Country is required.");

        if (request.Country.Trim().Length > 100)
            throw new InvalidOperationException("Country must be 100 characters or fewer.");

        if (!string.IsNullOrWhiteSpace(request.Website) && request.Website.Trim().Length > 500)
            throw new InvalidOperationException("Website must be 500 characters or fewer.");

        if (request.Kind == RegulatoryBodyKind.Standard && request.SectorId is null)
            throw new InvalidOperationException("Standard regulatory bodies must specify a SectorId.");

        if (request.Kind == RegulatoryBodyKind.Regulation && request.SectorId is not null)
            throw new InvalidOperationException("Regulation regulatory bodies must not specify a SectorId.");

        var normalizedCode = request.Code.Trim();
        var codeExists = await _dbContext.RegulatoryBodies
            .AnyAsync(b => b.Code == normalizedCode, cancellationToken);

        if (codeExists)
            throw new InvalidOperationException($"A regulatory body with code '{normalizedCode}' already exists.");

        Domain.Entities.Sector? sector = null;
        if (request.SectorId is not null)
        {
            sector = await _dbContext.Sectors
                .FirstOrDefaultAsync(s => s.Id == request.SectorId.Value, cancellationToken)
                ?? throw new InvalidOperationException($"Sector {request.SectorId} not found");
        }

        var body = new Domain.Entities.RegulatoryBody
        {
            Name = request.Name.Trim(),
            Code = normalizedCode,
            Country = request.Country.Trim(),
            Website = string.IsNullOrWhiteSpace(request.Website) ? null : request.Website.Trim(),
            Kind = request.Kind,
            SectorId = request.SectorId,
        };

        // Defence in depth alongside the DB check constraint (ck_regulatory_bodies_kind_sector).
        body.ValidateSectorConsistency();

        _dbContext.RegulatoryBodies.Add(body);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created regulatory body {BodyId}: {Name} ({Kind})", body.Id, body.Name, body.Kind);

        return new RegulatoryBodyDto
        {
            Id = body.Id,
            Name = body.Name,
            Code = body.Code,
            Country = body.Country,
            Kind = body.Kind.ToString(),
            SectorId = body.SectorId,
            SectorName = sector?.Name,
        };
    }

    public async Task<RegulatoryDocumentListDto> CreateDocumentAsync(
        CreateRegulatoryDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("Title is required.");

        if (request.Title.Trim().Length > 500)
            throw new InvalidOperationException("Title must be 500 characters or fewer.");

        if (string.IsNullOrWhiteSpace(request.Version))
            throw new InvalidOperationException("Version is required.");

        if (request.Version.Trim().Length > 50)
            throw new InvalidOperationException("Version must be 50 characters or fewer.");

        var body = await _dbContext.RegulatoryBodies
            .FirstOrDefaultAsync(b => b.Id == request.RegulatoryBodyId, cancellationToken)
            ?? throw new InvalidOperationException($"Regulatory body {request.RegulatoryBodyId} not found");

        if (!string.IsNullOrWhiteSpace(request.SourceUrl)
            && !SourceUrlValidator.IsValid(request.SourceUrl, out var validationError))
        {
            throw new InvalidSourceUrlException(validationError!);
        }

        var document = new Domain.Entities.RegulatoryDocument
        {
            RegulatoryBodyId = request.RegulatoryBodyId,
            Title = request.Title.Trim(),
            Version = request.Version.Trim(),
            SourceUrl = string.IsNullOrWhiteSpace(request.SourceUrl) ? null : request.SourceUrl.Trim(),
        };

        _dbContext.RegulatoryDocuments.Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created regulatory document {DocumentId}: {Title}", document.Id, document.Title);

        return new RegulatoryDocumentListDto
        {
            Id = document.Id,
            RegulatoryBodyName = body.Name,
            RegulatoryBodyCode = body.Code,
            Title = document.Title,
            Version = document.Version,
            Source = document.Source,
            SourceUrl = document.SourceUrl,
            EffectiveDate = document.EffectiveDate,
            IsActive = document.IsActive,
            LastIngestedAt = document.LastIngestedAt,
            SectorKeys = new List<string>(),
            DraftCount = 0,
            ApprovedCount = 0,
            RejectedCount = 0,
        };
    }

    private async Task<IngestionSessionDto> BuildIngestionSessionDto(
        Domain.Entities.RegulatoryDocument document,
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
            Status = document.LastIngestionStatus.ToString(),
            LastIngestedAt = document.LastIngestedAt,
            LastIngestionErrorMessage = document.LastIngestionErrorMessage,
            LastIngestionErrorCode = document.LastIngestionErrorCode,
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
