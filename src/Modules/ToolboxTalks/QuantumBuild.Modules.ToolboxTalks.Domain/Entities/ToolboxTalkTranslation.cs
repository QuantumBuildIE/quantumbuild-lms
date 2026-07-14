using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Represents a translated version of a toolbox talk in a specific language.
/// Translations include title, description, sections, questions, and email templates.
/// </summary>
public class ToolboxTalkTranslation : TenantEntity
{
    /// <summary>
    /// Reference to the original toolbox talk
    /// </summary>
    public Guid ToolboxTalkId { get; set; }

    /// <summary>
    /// ISO 639-1 language code (e.g., "es", "fr", "pl", "ro")
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// Translated title of the toolbox talk
    /// </summary>
    public string TranslatedTitle { get; set; } = string.Empty;

    /// <summary>
    /// Translated description of the toolbox talk
    /// </summary>
    public string? TranslatedDescription { get; set; }

    /// <summary>
    /// JSON array of translated sections: [{SectionId, Title, Content}]
    /// </summary>
    public string TranslatedSections { get; set; } = "[]";

    /// <summary>
    /// JSON array of translated questions and answers for the quiz
    /// </summary>
    public string? TranslatedQuestions { get; set; }

    /// <summary>
    /// Translated email subject for notifications
    /// </summary>
    public string EmailSubject { get; set; } = string.Empty;

    /// <summary>
    /// Translated email body for notifications
    /// </summary>
    public string EmailBody { get; set; } = string.Empty;

    /// <summary>
    /// When this translation was created/generated
    /// </summary>
    public DateTime TranslatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Provider used for translation (e.g., "Claude", "Manual", "GoogleTranslate")
    /// </summary>
    public string TranslationProvider { get; set; } = string.Empty;

    /// <summary>
    /// True when the English source text for one or more sections has been edited via a reviewer
    /// acceptance and these translations have not yet been re-validated against the new source.
    /// </summary>
    public bool NeedsRevalidation { get; set; } = false;

    /// <summary>
    /// When an external reviewer's accepted submission was last auto-applied to this translation.
    /// Null when no external review round has ever been applied.
    /// </summary>
    public DateTime? LastExternalReviewedAt { get; set; }

    /// <summary>
    /// Email address of the external reviewer whose accepted edits were last auto-applied
    /// (sourced from <c>ExternalParticipantInvitation.InvitedEmail</c> — no reviewer name is ever collected).
    /// </summary>
    public string? LastExternalReviewedBy { get; set; }

    // Navigation properties

    /// <summary>
    /// The original toolbox talk that was translated
    /// </summary>
    public ToolboxTalk ToolboxTalk { get; set; } = null!;
}
