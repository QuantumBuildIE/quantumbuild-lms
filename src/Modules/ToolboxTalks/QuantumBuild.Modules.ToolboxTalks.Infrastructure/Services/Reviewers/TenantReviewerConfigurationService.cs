using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Reviewers;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Reviewers;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Reviewers;

public class TenantReviewerConfigurationService(
    IToolboxTalksDbContext dbContext,
    ILanguageCodeService languageCodeService,
    ILogger<TenantReviewerConfigurationService> logger) : ITenantReviewerConfigurationService
{
    public async Task<List<TenantReviewerConfigurationDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var configurations = await dbContext.TenantReviewerConfigurations
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        // Null-language (fallback) row first, then alphabetical by language code
        return configurations
            .OrderBy(c => c.LanguageCode is null ? 0 : 1)
            .ThenBy(c => c.LanguageCode, StringComparer.OrdinalIgnoreCase)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<TenantReviewerConfigurationDto> CreateAsync(
        Guid tenantId,
        string? languageCode,
        string reviewerEmail,
        string? reviewerName,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);

        if (normalizedLanguageCode != null)
        {
            var allLanguages = await languageCodeService.GetAllLanguagesAsync();
            if (allLanguages.Count > 0
                && !allLanguages.Values.Contains(normalizedLanguageCode, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"VALIDATION:Language code '{normalizedLanguageCode}' is not in the supported language list.");
            }
        }

        var conflict = await dbContext.TenantReviewerConfigurations
            .IgnoreQueryFilters()
            .AnyAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.LanguageCode == normalizedLanguageCode, cancellationToken);

        if (conflict)
        {
            var label = normalizedLanguageCode ?? "all languages (fallback)";
            throw new InvalidOperationException($"CONFLICT:A reviewer configuration for {label} already exists.");
        }

        var entity = new TenantReviewerConfiguration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LanguageCode = normalizedLanguageCode,
            ReviewerEmail = reviewerEmail.Trim(),
            ReviewerName = NormalizeName(reviewerName),
        };

        dbContext.TenantReviewerConfigurations.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created TenantReviewerConfiguration: TenantId={TenantId}, LanguageCode={LanguageCode}",
            tenantId, normalizedLanguageCode ?? "(fallback)");

        return MapToDto(entity);
    }

    public async Task<TenantReviewerConfigurationDto> UpdateAsync(
        Guid tenantId,
        Guid id,
        string reviewerEmail,
        string? reviewerName,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.TenantReviewerConfigurations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);

        if (entity == null)
            throw new InvalidOperationException("Reviewer configuration not found.");

        entity.ReviewerEmail = reviewerEmail.Trim();
        entity.ReviewerName = NormalizeName(reviewerName);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Updated TenantReviewerConfiguration: TenantId={TenantId}, Id={Id}",
            tenantId, id);

        return MapToDto(entity);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await dbContext.TenantReviewerConfigurations
            .IgnoreQueryFilters()
            .Where(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
            throw new InvalidOperationException("Reviewer configuration not found.");

        logger.LogInformation(
            "Deleted TenantReviewerConfiguration: TenantId={TenantId}, Id={Id}",
            tenantId, id);
    }

    private static string? NormalizeLanguageCode(string? languageCode) =>
        string.IsNullOrWhiteSpace(languageCode) ? null : languageCode.Trim().ToLowerInvariant();

    private static string? NormalizeName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim();

    private static TenantReviewerConfigurationDto MapToDto(TenantReviewerConfiguration entity) => new()
    {
        Id = entity.Id,
        LanguageCode = entity.LanguageCode,
        ReviewerEmail = entity.ReviewerEmail,
        ReviewerName = entity.ReviewerName,
    };
}
