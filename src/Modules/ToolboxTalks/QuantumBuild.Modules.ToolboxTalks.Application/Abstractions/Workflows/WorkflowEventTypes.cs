namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;

public static class WorkflowEventTypes
{
    public const string TranslationStarted = "TranslationStarted";
    public const string TranslationCompleted = "TranslationCompleted";
    public const string ValidationStarted = "ValidationStarted";
    public const string ValidationCompleted = "ValidationCompleted";
    public const string InternalReviewSubmitted = "InternalReviewSubmitted";
    public const string ExternalReviewInitiated = "ExternalReviewInitiated";
    public const string ExternalReviewSubmitted = "ExternalReviewSubmitted";
    public const string ExternalReviewConfirmed = "ExternalReviewConfirmed";
    public const string ExternalReviewRejected = "ExternalReviewRejected";
    public const string ExternalReviewCancelled = "ExternalReviewCancelled";
    public const string ExternalReviewDeclined = "ExternalReviewDeclined";
    public const string AcceptedAsFinal = "AcceptedAsFinal";
    public const string MarkedStale = "MarkedStale";
}
