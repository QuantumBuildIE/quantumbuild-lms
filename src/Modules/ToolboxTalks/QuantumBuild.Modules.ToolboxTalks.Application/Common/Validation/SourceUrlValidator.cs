namespace QuantumBuild.Modules.ToolboxTalks.Application.Common.Validation;

/// <summary>
/// Validates that a regulatory document SourceUrl is a fetchable absolute http/https URL.
/// Uri.TryCreate with UriKind.Absolute will happily parse a Windows path (e.g. "C:\foo\bar.pdf")
/// or a "file://" URI as valid — it just resolves to scheme "file" — so an explicit scheme
/// check is required in addition to TryCreate succeeding.
/// Shared between RequirementIngestionService (pre-enqueue gate, called from the controller's
/// request path) and RequirementIngestionJob (defensive re-check for documents whose SourceUrl
/// was written before this validation existed).
/// </summary>
public static class SourceUrlValidator
{
    public static bool IsValid(string? sourceUrl, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            errorMessage = "Source URL is required.";
            return false;
        }

        if (!Uri.TryCreate(sourceUrl.Trim(), UriKind.Absolute, out var uri))
        {
            errorMessage = "Source URL must be a valid absolute URL (e.g. https://example.com/document.pdf).";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            errorMessage = $"Source URL must use http or https — '{uri.Scheme}' is not supported.";
            return false;
        }

        errorMessage = null;
        return true;
    }
}
