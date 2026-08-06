namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Pdf;

/// <summary>
/// Result of a PDF text extraction operation
/// </summary>
public record PdfExtractionResult(
    bool Success,
    string? Text,
    int PageCount,
    string? ErrorMessage,
    string? ErrorCategory = null)
{
    /// <summary>
    /// Creates a successful extraction result
    /// </summary>
    public static PdfExtractionResult SuccessResult(string text, int pageCount) =>
        new(
            Success: true,
            Text: text,
            PageCount: pageCount,
            ErrorMessage: null,
            ErrorCategory: null);

    /// <summary>
    /// Creates a failed extraction result. <paramref name="errorCategory"/> should be one of
    /// the <see cref="PdfExtractionErrorCategory"/> constants — defaults to Unknown so a caller
    /// that forgets to categorise still gets an honest "unknown" rather than a false category.
    /// </summary>
    public static PdfExtractionResult FailureResult(
        string errorMessage,
        string errorCategory = PdfExtractionErrorCategory.Unknown) =>
        new(
            Success: false,
            Text: null,
            PageCount: 0,
            ErrorMessage: errorMessage,
            ErrorCategory: errorCategory);
}
