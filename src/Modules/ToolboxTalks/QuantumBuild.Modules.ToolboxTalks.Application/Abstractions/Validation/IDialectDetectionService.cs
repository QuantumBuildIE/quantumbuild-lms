namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

/// <summary>
/// Detects the regional dialect/variant of a text sample using Claude Haiku.
/// </summary>
public interface IDialectDetectionService
{
    /// <summary>
    /// Analyses a text sample and detects its regional dialect.
    /// </summary>
    /// <param name="text">The text sample to analyse</param>
    /// <param name="expectedLanguageCode">ISO language code hint (e.g. "pt", "en")</param>
    /// <param name="tenantId">Tenant ID for AI usage logging</param>
    /// <param name="userId">User ID for AI usage logging (null for system calls)</param>
    /// <param name="toolboxTalkId">Reference entity ID for AI usage logging</param>
    /// <param name="isSystemCall">Whether this is a background/system call</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dialect detection result</returns>
    Task<DialectDetectionResult> DetectAsync(
        string text,
        string expectedLanguageCode,
        Guid tenantId,
        Guid? userId = null,
        Guid? toolboxTalkId = null,
        bool isSystemCall = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of dialect detection for a text sample.
/// </summary>
public class DialectDetectionResult
{
    /// <summary>
    /// Whether detection completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if detection failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Detected ISO language code (e.g. "pt", "en", "es").
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// Regional variant description (e.g. "Brazilian Portuguese", "British English").
    /// </summary>
    public string Variant { get; set; } = string.Empty;

    /// <summary>
    /// Confidence level of the detection.
    /// </summary>
    public DialectConfidence Confidence { get; set; }

    /// <summary>
    /// Short explanation of the dialect indicators found.
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// Instructions for calibrating back-translation to match this dialect.
    /// </summary>
    public string BackTranslationGuidance { get; set; } = string.Empty;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static DialectDetectionResult SuccessResult(
        string languageCode,
        string variant,
        DialectConfidence confidence,
        string reasoning,
        string backTranslationGuidance) =>
        new()
        {
            Success = true,
            LanguageCode = languageCode,
            Variant = variant,
            Confidence = confidence,
            Reasoning = reasoning,
            BackTranslationGuidance = backTranslationGuidance
        };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static DialectDetectionResult FailureResult(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}

/// <summary>
/// Confidence level for dialect detection.
/// </summary>
public enum DialectConfidence
{
    /// <summary>Few dialect indicators found; detection is uncertain.</summary>
    Low,

    /// <summary>Some dialect indicators found; detection is probable.</summary>
    Medium,

    /// <summary>Strong dialect indicators found; detection is reliable.</summary>
    High
}
