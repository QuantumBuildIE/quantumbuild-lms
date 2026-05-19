namespace QuantumBuild.Core.Application.Abstractions;

public interface IBulkEmployeeImportJob
{
    /// <summary>
    /// Creates employee (and optionally user) records for every Valid/Warning row in a
    /// Validated BulkImportSession, then sends invitation emails at a rate that stays
    /// within the email provider limit.
    /// </summary>
    Task ExecuteAsync(Guid sessionId, CancellationToken ct);
}
