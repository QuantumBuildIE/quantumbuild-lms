using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.SendForReview;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Common;

/// <summary>
/// Resolves a tenant's configured reviewer for a language: an exact TenantReviewerConfiguration
/// match on LanguageCode first, then the tenant's null-language fallback row, else none.
/// Operates on an already-loaded configuration set so callers can fetch once per tenant and
/// resolve for every language in a talk without re-querying.
/// </summary>
public static class ReviewerResolution
{
    public static (TenantReviewerConfiguration? Config, ReviewerResolutionSource Source) Resolve(
        IReadOnlyCollection<TenantReviewerConfiguration> configurations,
        string languageCode)
    {
        var normalized = languageCode.Trim();

        var specific = configurations.FirstOrDefault(c =>
            c.LanguageCode != null && c.LanguageCode.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (specific is not null)
            return (specific, ReviewerResolutionSource.LanguageSpecific);

        var fallback = configurations.FirstOrDefault(c => c.LanguageCode is null);
        if (fallback is not null)
            return (fallback, ReviewerResolutionSource.Fallback);

        return (null, ReviewerResolutionSource.None);
    }
}
