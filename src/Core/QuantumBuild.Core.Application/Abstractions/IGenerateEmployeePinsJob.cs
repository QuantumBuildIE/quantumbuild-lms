namespace QuantumBuild.Core.Application.Abstractions;

public interface IGenerateEmployeePinsJob
{
    /// <summary>
    /// Generates QR PINs for all active employees in <paramref name="tenantId"/>
    /// that do not yet have a PIN set, and emails each one their PIN.
    /// Enqueued once when QrLocationTrainingEnabled is first set to true.
    /// </summary>
    Task ExecuteAsync(Guid tenantId, CancellationToken ct);
}
