using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Top-level record for a translation validation session tied to a Talk or Course.
/// </summary>
public class TranslationValidationRun : TenantEntity
{
    // Foreign keys (one or the other, not both)
    public Guid? ToolboxTalkId { get; set; }
    public Guid? CourseId { get; set; }

    // Validation configuration
    public string LanguageCode { get; set; } = string.Empty;
    public string? SectorKey { get; set; }
    public int PassThreshold { get; set; }
    public string SourceLanguage { get; set; } = string.Empty;
    public string? SourceDialect { get; set; }

    // Aggregate results
    public int OverallScore { get; set; }
    public ValidationOutcome OverallOutcome { get; set; }
    public ValidationOutcome? SafetyVerdict { get; set; }
    public int TotalSections { get; set; }
    public int PassedSections { get; set; }
    public int ReviewSections { get; set; }
    public int FailedSections { get; set; }

    // Audit metadata
    public string? ReviewerName { get; set; }
    public string? ReviewerOrg { get; set; }
    public string? ReviewerRole { get; set; }
    public string? DocumentRef { get; set; }
    public string? ClientName { get; set; }
    public string? AuditPurpose { get; set; }

    // Pre-flight scan
    public string? PreFlightScanJson { get; set; }

    // Run state
    public ValidationRunStatus Status { get; set; } = ValidationRunStatus.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? AuditReportUrl { get; set; }

    // Navigation properties
    public ToolboxTalk? ToolboxTalk { get; set; }
    public ToolboxTalkCourse? Course { get; set; }
    public ICollection<TranslationValidationResult> Results { get; set; } = new List<TranslationValidationResult>();
}
