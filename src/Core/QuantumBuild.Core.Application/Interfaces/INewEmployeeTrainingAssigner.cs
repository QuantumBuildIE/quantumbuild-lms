namespace QuantumBuild.Core.Application.Interfaces;

/// <summary>
/// Interface for auto-assigning training when a new employee is created.
/// Implemented by modules that provide training (e.g., Learnings).
/// </summary>
public interface INewEmployeeTrainingAssigner
{
    Task AssignNewEmployeeTrainingAsync(
        Guid tenantId,
        Guid employeeId,
        DateTime? startDate = null,
        CancellationToken ct = default);
}
