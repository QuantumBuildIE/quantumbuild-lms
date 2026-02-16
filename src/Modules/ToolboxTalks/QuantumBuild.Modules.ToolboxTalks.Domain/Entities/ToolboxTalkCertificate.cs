using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Represents a certificate issued to an employee upon completing a toolbox talk or course.
/// Stores snapshot data at time of issue for immutable record keeping.
/// </summary>
public class ToolboxTalkCertificate : TenantEntity
{
    /// <summary>
    /// The employee who earned the certificate
    /// </summary>
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// Type of certificate (Talk or Course)
    /// </summary>
    public CertificateType CertificateType { get; set; }

    // For Talk certificates

    /// <summary>
    /// The toolbox talk this certificate was issued for (Talk certificates only)
    /// </summary>
    public Guid? ToolboxTalkId { get; set; }

    /// <summary>
    /// The scheduled talk instance that was completed (Talk certificates only)
    /// </summary>
    public Guid? ScheduledTalkId { get; set; }

    // For Course certificates

    /// <summary>
    /// The course this certificate was issued for (Course certificates only)
    /// </summary>
    public Guid? CourseId { get; set; }

    /// <summary>
    /// The course assignment that was completed (Course certificates only)
    /// </summary>
    public Guid? CourseAssignmentId { get; set; }

    /// <summary>
    /// Unique certificate number (e.g., CERT-TBT-20260211-001)
    /// </summary>
    public string CertificateNumber { get; set; } = string.Empty;

    /// <summary>
    /// When the certificate was issued
    /// </summary>
    public DateTime IssuedAt { get; set; }

    /// <summary>
    /// When the certificate expires (null if no expiry)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Storage path for the generated PDF certificate
    /// </summary>
    public string PdfStoragePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether this certificate was issued for a refresher completion
    /// </summary>
    public bool IsRefresher { get; set; } = false;

    // Snapshot of data at time of issue

    /// <summary>
    /// Employee name at time of certificate issue
    /// </summary>
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>
    /// Employee code at time of certificate issue
    /// </summary>
    public string? EmployeeCode { get; set; }

    /// <summary>
    /// Title of the training (talk or course name) at time of issue
    /// </summary>
    public string TrainingTitle { get; set; } = string.Empty;

    /// <summary>
    /// JSON snapshot of included talks for course certificates
    /// </summary>
    public string? IncludedTalksJson { get; set; }

    /// <summary>
    /// Base64 data URL of the employee's signature
    /// </summary>
    public string? SignatureDataUrl { get; set; }

    // Navigation properties

    /// <summary>
    /// The employee who earned the certificate
    /// </summary>
    public Employee Employee { get; set; } = null!;

    /// <summary>
    /// The toolbox talk (Talk certificates only)
    /// </summary>
    public ToolboxTalk? ToolboxTalk { get; set; }

    /// <summary>
    /// The course (Course certificates only)
    /// </summary>
    public ToolboxTalkCourse? Course { get; set; }
}
