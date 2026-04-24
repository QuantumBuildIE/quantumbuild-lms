using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;

/// <summary>
/// Manages the system-level translation pipeline version record.
/// Provides a single source of truth for what configuration was active when a validation run executed.
/// </summary>
public interface IPipelineVersionService
{
    /// <summary>
    /// Returns the currently active pipeline version, or null if none exists.
    /// </summary>
    Task<PipelineVersion?> GetActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the active pipeline version that matches the current appsettings configuration.
    /// If no matching active version exists, creates one (deactivating any previous active record first).
    /// Safe to call repeatedly — idempotent when the configuration has not changed.
    /// </summary>
    Task<PipelineVersion> GetOrCreateCurrentAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates a new pipeline version record for the given version string using the
    /// current appsettings configuration. Deactivates the previously active version.
    /// Called when an admin records a deliberate pipeline change.
    /// </summary>
    Task<PipelineVersion> CreateNewVersionAsync(string version, CancellationToken ct = default);

    /// <summary>
    /// Creates an append-only PipelineChangeRecord, bumps the active pipeline version,
    /// and returns the persisted change record with a sequential ChangeId (e.g. "CR-0001").
    /// </summary>
    Task<PipelineChangeRecord> CreateChangeRecordAsync(
        CreatePipelineChangeRecordRequest request, CancellationToken ct = default);
}
