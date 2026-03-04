using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

/// <summary>
/// Generates a formal PDF audit report for a completed translation validation run.
/// </summary>
public interface IValidationReportService
{
    /// <summary>
    /// Generates a PDF audit report for the given validation run.
    /// The run must include its Results collection and the associated ToolboxTalk.
    /// </summary>
    Task<byte[]> GenerateAsync(TranslationValidationRun run, CancellationToken ct = default);
}
