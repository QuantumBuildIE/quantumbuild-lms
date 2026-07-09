namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Workflows;

public record ExternalReviewPortalDto
{
    public string TalkTitle { get; init; } = string.Empty;
    public string LanguageCode { get; init; } = string.Empty;
    public string LanguageName { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    /// <summary>"Active" | "Used" | "Revoked" | "Expired" | "Unknown"</summary>
    public string PortalStatus { get; init; } = string.Empty;
    public string ContextType { get; init; } = string.Empty;
    public int FlaggedWordCount { get; init; }
    public IReadOnlyList<ExternalReviewSectionDto> Sections { get; init; } = [];
    /// <summary>Null = no restriction, all sections are editable. Non-null = only these section indices are editable.</summary>
    public IReadOnlyList<int>? EditableSectionIndices { get; init; }
}

public record ExternalReviewSectionDto
{
    public int SectionIndex { get; init; }
    public string SectionTitle { get; init; } = string.Empty;
    public string OriginalText { get; init; } = string.Empty;
    public string TranslatedText { get; init; } = string.Empty;
    public IReadOnlyList<ExternalReviewFlagDto> Flags { get; init; } = [];
}

public record ExternalReviewFlagDto
{
    public int StartOffset { get; init; }
    public int EndOffset { get; init; }
    public string Severity { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}
