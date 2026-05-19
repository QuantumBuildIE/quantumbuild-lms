namespace QuantumBuild.Core.Domain.Enums;

public enum BulkImportStatus
{
    /// <summary>CSV uploaded to R2 storage, awaiting validation pass.</summary>
    Uploaded = 1,

    /// <summary>CSV has been validated; ValidationResultJson is populated.</summary>
    Validated = 2,

    // Stage 2: Hangfire job transitions the session through the remaining statuses.
    /// <summary>Hangfire job is actively creating employee/user records.</summary>
    Processing = 3,

    /// <summary>All records created successfully; CSV deleted from R2.</summary>
    Completed = 4,

    /// <summary>Hangfire job failed; see ValidationResultJson for context.</summary>
    Failed = 5,

    /// <summary>Cancelled by an admin before processing began.</summary>
    Cancelled = 6
}
