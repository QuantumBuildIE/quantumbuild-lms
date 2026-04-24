using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Application.Abstractions;

public enum PinVerificationStatus
{
    Success,
    Failed,
    Locked
}

public record PinVerificationResult(
    PinVerificationStatus Status,
    int AttemptsRemaining,
    DateTimeOffset? LockedUntil);

public interface IEmployeePinService
{
    /// <summary>
    /// Generates a cryptographically random 6-digit PIN string (zero-padded, not 000000, not starting with 000).
    /// </summary>
    string GenerateRawPin();

    /// <summary>
    /// Hashes rawPin and persists it on the employee record. Returns the raw PIN.
    /// </summary>
    Task<string> SetPinAsync(Employee employee, string rawPin, CancellationToken ct = default);

    /// <summary>
    /// Verifies rawPin against the stored hash, enforcing lockout after 5 failures.
    /// </summary>
    Task<PinVerificationResult> VerifyPinAsync(Employee employee, string rawPin, CancellationToken ct = default);

    /// <summary>
    /// Generates a new PIN, sets it, and returns the raw PIN.
    /// </summary>
    Task<string> ResetPinAsync(Employee employee, CancellationToken ct = default);
}
