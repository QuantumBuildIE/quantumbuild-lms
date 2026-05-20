namespace QuantumBuild.Core.Application.Features.BulkImport;

public enum BulkImportRowStatus
{
    Valid = 1,
    Warning = 2,
    Failed = 3
}

/// <summary>
/// Top-level result returned by IBulkEmployeeImportValidationService.ValidateAsync.
/// Serialised to BulkImportSession.ValidationResultJson; deserialised by the Stage 2 job.
/// </summary>
public sealed record BulkImportValidationResult
{
    public int TotalRows { get; init; }
    public int ValidRows { get; init; }
    public int WarningRows { get; init; }
    public int FailedRows { get; init; }
    public List<BulkImportRowResult> Rows { get; init; } = new();
}

/// <summary>
/// Per-row outcome. Normalised field values are populated for Valid and Warning rows;
/// for Failed rows they contain whatever could be parsed before the first hard failure.
/// Stage 2: the Hangfire job only processes rows where Status is Valid or Warning.
/// </summary>
public sealed record BulkImportRowResult
{
    /// <summary>1-based row number matching the spreadsheet (header = row 1, first data row = 2).</summary>
    public int RowNumber { get; init; }
    public BulkImportRowStatus Status { get; init; }
    public List<string> Messages { get; init; } = new();

    // Normalised values — used by Stage 2 to create Employee and (optionally) User records.
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public bool CreateUserAccount { get; init; }
    public string? Phone { get; init; }
    public string? Mobile { get; init; }
    public string? JobTitle { get; init; }
    public string? Department { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public string? Notes { get; init; }
    public string PreferredLanguage { get; init; } = "en";

    /// <summary>
    /// Normalised to "Operator" or "Supervisor". Only meaningful when CreateUserAccount is true.
    /// Stage 2: ignored when CreateUserAccount is false.
    /// </summary>
    public string UserRole { get; init; } = "Operator";
}
