namespace QuantumBuild.Modules.ToolboxTalks.Application.Services.Storage;

/// <summary>
/// Service for generating URL-safe slugs from titles.
/// </summary>
public interface ISlugGeneratorService
{
    /// <summary>
    /// Generates a URL-safe slug from a title.
    /// Example: "Working at Heights Safety" -> "working-at-heights-safety"
    /// </summary>
    /// <param name="title">The title to slugify</param>
    /// <returns>URL-safe slug (lowercase, hyphens, no special chars)</returns>
    string GenerateSlug(string title);

    /// <summary>
    /// Generates a file name with slug and ID for uniqueness.
    /// Example: "My Talk", guid, "mp4" -> "my-talk_3a26b58d.mp4"
    /// </summary>
    /// <param name="title">The title to slugify</param>
    /// <param name="id">The unique ID (first 8 chars of GUID will be used)</param>
    /// <param name="extension">File extension (without dot)</param>
    /// <returns>File name like "my-title_abc123.mp4"</returns>
    string GenerateFileName(string title, Guid id, string extension);
}
