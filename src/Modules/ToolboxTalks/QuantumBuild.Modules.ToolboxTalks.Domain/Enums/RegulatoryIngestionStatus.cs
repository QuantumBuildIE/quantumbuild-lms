namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Ingestion state of a RegulatoryDocument's most recent AI extraction attempt.
/// Distinct from RequirementIngestionStatus, which tracks the approval state of an
/// individual extracted requirement.
/// </summary>
public enum RegulatoryIngestionStatus
{
    /// <summary>
    /// No ingestion attempt has ever run for this document.
    /// </summary>
    Idle = 1,

    /// <summary>
    /// An ingestion job is currently running for this document.
    /// </summary>
    Ingesting = 2,

    /// <summary>
    /// The most recent ingestion attempt completed successfully (including the case
    /// where zero requirements were extracted from valid content).
    /// </summary>
    Success = 3,

    /// <summary>
    /// The most recent ingestion attempt failed. See LastIngestionErrorCode and
    /// LastIngestionErrorMessage on RegulatoryDocument for details.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// The most recent ingestion attempt did not persist anything because the document had no
    /// active RegulatoryProfile to attach requirements to — a non-fatal, expected state for a
    /// freshly created document, not an extraction failure. Distinct from Failed so it reads as
    /// "nothing to do here" rather than "something went wrong". See LastIngestionErrorCode and
    /// LastIngestionErrorMessage on RegulatoryDocument for details.
    /// </summary>
    Skipped = 5
}
