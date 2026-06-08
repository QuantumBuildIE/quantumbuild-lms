namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

public enum TranslationWorkflowState
{
    /// <summary>No events have been recorded for this language.</summary>
    Initial = 0,
    /// <summary>AI translation has been completed and is ready for validation.</summary>
    AIGenerated = 1,
    /// <summary>Back-translation validation has been completed.</summary>
    Validated = 2,
    /// <summary>An internal reviewer has accepted or edited the translation.</summary>
    ReviewerAccepted = 3,
    /// <summary>An external reviewer invitation has been sent and is pending submission.</summary>
    AwaitingThirdParty = 4,
    /// <summary>The external reviewer has submitted their review.</summary>
    ThirdPartyReviewed = 5,
    /// <summary>The translation has been accepted as final.</summary>
    Accepted = 6,
    /// <summary>The translation has been marked stale and requires re-translation.</summary>
    Stale = 7,
    /// <summary>
    /// A translation job is currently in progress (transient state).
    /// Entered when a TranslationStarted event is recorded; exited when
    /// a TranslationCompleted event is recorded (which transitions to AIGenerated).
    /// </summary>
    Translating = 8
}
