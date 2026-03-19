using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Mapping;

/// <summary>
/// Generates an Inspection Readiness Report PDF from compliance checklist data
/// and stores it in R2 storage.
/// </summary>
public interface IInspectionReportService
{
    /// <summary>
    /// Generates the inspection readiness report PDF for the given sector,
    /// uploads it to R2, and returns the download URL with summary metadata.
    /// </summary>
    Task<InspectionReportResultDto> GenerateReportAsync(
        string sectorKey,
        GenerateInspectionReportRequest request,
        CancellationToken cancellationToken = default);
}
