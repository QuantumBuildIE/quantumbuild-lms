namespace QuantumBuild.Core.Domain.Enums;

public enum BulkSopImportStatus
{
    /// <summary>ZIP uploaded to R2 storage, awaiting validation pass.</summary>
    Uploaded = 1,

    /// <summary>ZIP has been validated; ValidationResultJson is populated.</summary>
    Validated = 2,

    /// <summary>Hangfire job is actively creating learnings from the validated PDFs.</summary>
    Processing = 3,

    /// <summary>The job finished running. Individual items may still have failed — see
    /// ProcessingResultJson for the per-item partial-success breakdown.</summary>
    Completed = 4,

    /// <summary>Hangfire job failed at the job level (not a per-item failure); see ProcessingResultJson
    /// for whatever partial outcomes were recorded before the failure.</summary>
    Failed = 5,

    /// <summary>Cancelled by an admin before processing began.</summary>
    Cancelled = 6
}
