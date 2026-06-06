namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

public enum TranslationWorkflowState
{
    NotStarted = 0,
    AIGenerated = 1,
    ValidationComplete = 2,
    InternalReviewSubmitted = 3,
    AwaitingThirdParty = 4,
    ExternalReviewSubmitted = 5,
    ExternalReviewConfirmed = 6,
    Accepted = 7,
    Stale = 8
}
