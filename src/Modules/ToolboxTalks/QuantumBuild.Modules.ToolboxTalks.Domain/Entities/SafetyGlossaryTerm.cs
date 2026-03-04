using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Individual safety-critical term within a sector glossary.
/// Translations stored as JSON: {"pl":"ŚOI", "ro":"EIP", ...}
/// </summary>
public class SafetyGlossaryTerm : BaseEntity
{
    // Foreign key
    public Guid GlossaryId { get; set; }

    public string EnglishTerm { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsCritical { get; set; } = true;
    public string Translations { get; set; } = "{}";

    // Navigation property
    public SafetyGlossary Glossary { get; set; } = null!;
}
