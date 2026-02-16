namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Pdf;

/// <summary>
/// Result of a PDF text extraction operation
/// </summary>
public record PdfExtractionResult(
    bool Success,
    string? Text,
    int PageCount,
    string? ErrorMessage)
{
    /// <summary>
    /// Creates a successful extraction result
    /// </summary>
    public static PdfExtractionResult SuccessResult(string text, int pageCount) =>
        new(
            Success: true,
            Text: text,
            PageCount: pageCount,
            ErrorMessage: null);

    /// <summary>
    /// Creates a failed extraction result
    /// </summary>
    public static PdfExtractionResult FailureResult(string errorMessage) =>
        new(
            Success: false,
            Text: null,
            PageCount: 0,
            ErrorMessage: errorMessage);
}
