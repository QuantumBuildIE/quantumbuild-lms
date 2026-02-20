using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Reports;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Services;

/// <summary>
/// Service for generating PDF and Excel exports for toolbox talk reports
/// </summary>
public interface IToolboxTalkExportService
{
    /// <summary>
    /// Generate PDF compliance report
    /// </summary>
    /// <param name="data">Compliance report data</param>
    /// <returns>PDF file bytes</returns>
    Task<byte[]> GenerateComplianceReportPdfAsync(ComplianceReportDto data);

    /// <summary>
    /// Generate Excel export of overdue items
    /// </summary>
    /// <param name="data">List of overdue items</param>
    /// <returns>Excel file bytes</returns>
    Task<byte[]> GenerateOverdueReportExcelAsync(List<OverdueItemDto> data);

    /// <summary>
    /// Generate Excel export of completion details
    /// </summary>
    /// <param name="data">List of completion details</param>
    /// <returns>Excel file bytes</returns>
    Task<byte[]> GenerateCompletionsReportExcelAsync(List<CompletionDetailDto> data);

    /// <summary>
    /// Generate Excel export of skills matrix
    /// </summary>
    /// <param name="data">Skills matrix data</param>
    /// <returns>Excel file bytes</returns>
    Task<byte[]> GenerateSkillsMatrixExcelAsync(SkillsMatrixDto data);

    /// <summary>
    /// Generate completion certificate PDF for an individual completion
    /// </summary>
    /// <param name="completion">Completion record with related data</param>
    /// <param name="employeeName">Employee name</param>
    /// <param name="toolboxTalkTitle">Toolbox talk title</param>
    /// <returns>PDF file bytes</returns>
    Task<byte[]> GenerateCompletionCertificatePdfAsync(
        ScheduledTalkCompletion completion,
        string employeeName,
        string toolboxTalkTitle);
}
