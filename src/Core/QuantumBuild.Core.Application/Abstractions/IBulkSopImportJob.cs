namespace QuantumBuild.Core.Application.Abstractions;

public interface IBulkSopImportJob
{
    /// <summary>
    /// Creates one Draft learning per queued PDF in a Validated BulkSopImportSession, reusing
    /// the new learning-wizard's own initialise -> parse -> quiz-generate command chain.
    /// Each PDF is processed independently; a failure on one does not stop the others.
    /// </summary>
    Task ExecuteAsync(Guid sessionId, CancellationToken ct);
}
