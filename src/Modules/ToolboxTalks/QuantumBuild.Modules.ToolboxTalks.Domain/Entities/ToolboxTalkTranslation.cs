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

    // Navigation properties

    /// <summary>
    /// The original toolbox talk that was translated
    /// </summary>
    public ToolboxTalk ToolboxTalk { get; set; } = null!;
}
