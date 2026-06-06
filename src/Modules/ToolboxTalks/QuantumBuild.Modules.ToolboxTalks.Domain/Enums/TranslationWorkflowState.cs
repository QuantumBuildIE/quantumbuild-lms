namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

public enum TranslationWorkflowState
{
    Initial = 0,
    AIGenerated = 1,
    Validated = 2,
    ReviewerAccepted = 3,
    AwaitingThirdParty = 4,
    ThirdPartyReviewed = 5,
    Accepted = 6,
    Stale = 7
}
