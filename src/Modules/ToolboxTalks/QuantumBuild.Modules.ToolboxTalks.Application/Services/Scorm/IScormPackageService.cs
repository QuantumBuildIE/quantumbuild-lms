namespace QuantumBuild.Modules.ToolboxTalks.Application.Services.Scorm;

/// <summary>
/// Result of a SCORM package generation — the ZIP bytes plus enough talk metadata
/// for the caller to build a Content-Disposition filename without a second lookup.
/// </summary>
public sealed record ScormPackageResult(byte[] ZipBytes, string TalkTitle, string TalkCode);

/// <summary>
/// Generates SCORM 1.2 packages for standalone toolbox talks.
/// Chunk 1: minimal single-section, single-language, no-quiz, no-video package —
/// establishes manifest generation, ZIP layout, and the JS completion bridge that
/// later chunks (real content rendering, full JS bridge, quiz, video, multi-language) extend.
/// </summary>
public interface IScormPackageService
{
    /// <summary>
    /// Builds a minimal SCORM 1.2 package (imsmanifest.xml + index.html) for the given talk.
    /// Returns null if the talk does not exist for the given tenant.
    /// </summary>
    Task<ScormPackageResult?> GenerateMinimalPackageAsync(
        Guid talkId,
        Guid tenantId,
        string language,
        CancellationToken ct = default);
}
