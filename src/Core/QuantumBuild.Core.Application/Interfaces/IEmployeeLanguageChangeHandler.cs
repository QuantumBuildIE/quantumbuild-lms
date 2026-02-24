namespace QuantumBuild.Core.Application.Interfaces;

/// <summary>
/// Interface for handling employee language changes. Implemented by modules
/// that need to react when a new language is introduced to a tenant
/// (e.g., triggering translation generation for assigned content).
/// </summary>
public interface IEmployeeLanguageChangeHandler
{
    Task HandleLanguageChangeAsync(
        Guid tenantId,
        Guid employeeId,
        string preferredLanguage,
        CancellationToken ct = default);
}
