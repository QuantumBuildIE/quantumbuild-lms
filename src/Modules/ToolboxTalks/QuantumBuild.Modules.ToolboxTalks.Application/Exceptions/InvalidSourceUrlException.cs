namespace QuantumBuild.Modules.ToolboxTalks.Application.Exceptions;

/// <summary>
/// Thrown when a regulatory document SourceUrl fails validation (not an absolute http/https
/// URI) before an ingestion job would be enqueued. Distinct from the generic
/// InvalidOperationException ("document not found" / "no SourceUrl configured") so the
/// controller can attach the "invalid_uri" error code to the 400 response.
/// </summary>
public class InvalidSourceUrlException : Exception
{
    public const string ErrorCode = "invalid_uri";

    public InvalidSourceUrlException(string message) : base(message)
    {
    }
}
