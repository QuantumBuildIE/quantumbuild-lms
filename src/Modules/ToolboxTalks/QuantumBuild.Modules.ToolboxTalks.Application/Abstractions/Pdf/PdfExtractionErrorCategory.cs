namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Pdf;

/// <summary>
/// Failure reason categories for a PdfExtractionResult. Lets callers (e.g.
/// RequirementIngestionJob) map a PDF-fetch failure onto their own error-code taxonomy
/// without re-parsing free-text error messages.
/// </summary>
public static class PdfExtractionErrorCategory
{
    /// <summary>The URL uses a scheme HttpClient cannot fetch (e.g. "file").</summary>
    public const string UnsupportedScheme = "unsupported_scheme";

    /// <summary>The request reached the network layer but failed (DNS, connection refused, non-2xx status).</summary>
    public const string NetworkError = "network_error";

    /// <summary>The request timed out.</summary>
    public const string Timeout = "timeout";

    /// <summary>The document was fetched successfully but its content could not be parsed as PDF text.</summary>
    public const string ParseFailure = "parse_failure";

    /// <summary>Doesn't fit any of the above — logged honestly rather than forced into a category.</summary>
    public const string Unknown = "unknown";
}
