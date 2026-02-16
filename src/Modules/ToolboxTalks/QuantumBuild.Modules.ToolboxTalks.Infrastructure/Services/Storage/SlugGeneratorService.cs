using System.Text.RegularExpressions;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Storage;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Storage;

/// <summary>
/// Generates URL-safe slugs from titles for file naming.
/// </summary>
public partial class SlugGeneratorService : ISlugGeneratorService
{
    public string GenerateSlug(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "untitled";

        var slug = title.ToLowerInvariant();

        // Remove non-alphanumeric characters except spaces and hyphens
        slug = NonAlphanumericRegex().Replace(slug, "");

        // Replace spaces with hyphens
        slug = WhitespaceRegex().Replace(slug, "-");

        // Remove consecutive hyphens
        slug = ConsecutiveHyphensRegex().Replace(slug, "-");

        // Trim hyphens from start/end
        slug = slug.Trim('-');

        // Limit length to 50 characters (keeping at word boundary if possible)
        if (slug.Length > 50)
        {
            slug = slug[..50];
            var lastHyphen = slug.LastIndexOf('-');
            if (lastHyphen > 30) // Only trim at word boundary if we keep at least 30 chars
                slug = slug[..lastHyphen];
        }

        return string.IsNullOrEmpty(slug) ? "untitled" : slug;
    }

    public string GenerateFileName(string title, Guid id, string extension)
    {
        var slug = GenerateSlug(title);
        var shortId = id.ToString("N")[..8];
        var ext = extension.TrimStart('.');
        return $"{slug}_{shortId}.{ext}";
    }

    [GeneratedRegex(@"[^a-z0-9\s-]")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex ConsecutiveHyphensRegex();
}
