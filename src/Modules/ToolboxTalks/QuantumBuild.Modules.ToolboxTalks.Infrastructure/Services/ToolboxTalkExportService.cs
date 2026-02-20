using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Reports;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;

/// <summary>
/// Stub implementation of toolbox talk export service.
/// Full PDF/Excel generation to be implemented in Phase 2.
/// </summary>
public class ToolboxTalkExportService : IToolboxTalkExportService
{
    private readonly ILogger<ToolboxTalkExportService> _logger;

    public ToolboxTalkExportService(ILogger<ToolboxTalkExportService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<byte[]> GenerateComplianceReportPdfAsync(ComplianceReportDto data)
    {
        _logger.LogWarning("GenerateComplianceReportPdfAsync is not yet implemented. Returning empty PDF stub.");

        // TODO: Implement PDF generation using QuestPDF or similar library
        // This stub returns a minimal valid PDF for testing purposes
        var pdfStub = GenerateStubPdf("Compliance Report - Not Yet Implemented");
        return Task.FromResult(pdfStub);
    }

    /// <inheritdoc />
    public Task<byte[]> GenerateOverdueReportExcelAsync(List<OverdueItemDto> data)
    {
        _logger.LogWarning("GenerateOverdueReportExcelAsync is not yet implemented. Returning empty Excel stub.");

        // TODO: Implement Excel generation using ClosedXML
        // This stub returns a minimal valid XLSX for testing purposes
        var excelStub = GenerateStubExcel("Overdue Report");
        return Task.FromResult(excelStub);
    }

    /// <inheritdoc />
    public Task<byte[]> GenerateCompletionsReportExcelAsync(List<CompletionDetailDto> data)
    {
        _logger.LogWarning("GenerateCompletionsReportExcelAsync is not yet implemented. Returning empty Excel stub.");

        // TODO: Implement Excel generation using ClosedXML
        var excelStub = GenerateStubExcel("Completions Report");
        return Task.FromResult(excelStub);
    }

    /// <inheritdoc />
    public Task<byte[]> GenerateSkillsMatrixExcelAsync(SkillsMatrixDto data)
    {
        _logger.LogInformation("Generating Skills Matrix Excel export with {EmployeeCount} employees and {LearningCount} learnings",
            data.Employees.Count, data.Learnings.Count);

        using var workbook = new XLWorkbook();

        // --- Main "Skills Matrix" sheet ---
        var ws = workbook.Worksheets.Add("Skills Matrix");

        // Build cell lookup: (employeeId, learningId) -> cell
        var cellMap = new Dictionary<(Guid, Guid), SkillsMatrixCellDto>();
        foreach (var cell in data.Cells)
            cellMap[(cell.EmployeeId, cell.LearningId)] = cell;

        // Row 1: Headers
        var col = 1;
        ws.Cell(1, col++).Value = "Employee Code";
        ws.Cell(1, col++).Value = "Employee Name";
        ws.Cell(1, col++).Value = "Department";
        ws.Cell(1, col++).Value = "Job Title";

        var learningStartCol = col;
        foreach (var learning in data.Learnings)
        {
            var headerCell = ws.Cell(1, col);
            headerCell.Value = learning.Code;
            if (!string.IsNullOrWhiteSpace(learning.Title))
                headerCell.GetComment().AddText(learning.Title);
            col++;
        }

        // Style header row
        var headerRange = ws.Range(1, 1, 1, col - 1);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F2937");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        // Data rows
        var row = 2;
        foreach (var employee in data.Employees)
        {
            ws.Cell(row, 1).Value = employee.EmployeeCode;
            ws.Cell(row, 2).Value = employee.FullName;
            ws.Cell(row, 3).Value = employee.Department ?? "";
            ws.Cell(row, 4).Value = employee.JobTitle ?? "";

            var lCol = learningStartCol;
            foreach (var learning in data.Learnings)
            {
                var dataCell = ws.Cell(row, lCol);

                if (cellMap.TryGetValue((employee.Id, learning.Id), out var matrixCell))
                {
                    var (displayText, bgColor) = FormatSkillsMatrixCell(matrixCell);
                    dataCell.Value = displayText;
                    if (bgColor != null)
                    {
                        dataCell.Style.Fill.BackgroundColor = bgColor;
                    }
                    dataCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                lCol++;
            }
            row++;
        }

        // Auto-fit employee columns
        ws.Column(1).AdjustToContents();
        ws.Column(2).AdjustToContents();
        ws.Column(3).AdjustToContents();
        ws.Column(4).AdjustToContents();

        // Set learning columns to a fixed width
        for (var c = learningStartCol; c < learningStartCol + data.Learnings.Count; c++)
            ws.Column(c).Width = 16;

        // Freeze panes: freeze employee info columns and header row
        ws.SheetView.FreezeRows(1);
        ws.SheetView.FreezeColumns(4);

        // --- Legend sheet ---
        var legend = workbook.Worksheets.Add("Legend");
        legend.Cell(1, 1).Value = "Colour";
        legend.Cell(1, 2).Value = "Status";
        legend.Cell(1, 3).Value = "Description";
        var legendHeader = legend.Range(1, 1, 1, 3);
        legendHeader.Style.Font.Bold = true;
        legendHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F2937");
        legendHeader.Style.Font.FontColor = XLColor.White;

        var legendItems = new (string Label, string Hex, string Desc)[]
        {
            ("Completed", "#D1FAE5", "Employee has completed this learning (score shown if quiz taken)"),
            ("In Progress", "#DBEAFE", "Employee has started but not yet completed"),
            ("Overdue", "#FEE2E2", "Assignment is past due date (days overdue shown)"),
            ("Assigned", "#E0F2FE", "Assigned but not yet started"),
            ("Not Assigned", "#F9FAFB", "No assignment exists (cell left blank)"),
        };

        for (var i = 0; i < legendItems.Length; i++)
        {
            var r = i + 2;
            legend.Cell(r, 1).Style.Fill.BackgroundColor = XLColor.FromHtml(legendItems[i].Hex);
            legend.Cell(r, 2).Value = legendItems[i].Label;
            legend.Cell(r, 3).Value = legendItems[i].Desc;
        }

        legend.Column(1).Width = 10;
        legend.Column(2).AdjustToContents();
        legend.Column(3).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }

    private static (string DisplayText, XLColor? BackgroundColor) FormatSkillsMatrixCell(SkillsMatrixCellDto cell)
    {
        return cell.Status switch
        {
            "Completed" => (
                cell.Score.HasValue ? $"Completed ({cell.Score}%)" : "Completed",
                XLColor.FromHtml("#D1FAE5") // emerald-100
            ),
            "InProgress" => (
                "In Progress",
                XLColor.FromHtml("#DBEAFE") // blue-100
            ),
            "Overdue" => (
                cell.DaysOverdue.HasValue ? $"Overdue ({cell.DaysOverdue} days)" : "Overdue",
                XLColor.FromHtml("#FEE2E2") // red-100
            ),
            "Assigned" => (
                "Assigned",
                XLColor.FromHtml("#E0F2FE") // sky-100
            ),
            _ => ("", null) // NotAssigned — blank
        };
    }

    /// <inheritdoc />
    public Task<byte[]> GenerateCompletionCertificatePdfAsync(
        ScheduledTalkCompletion completion,
        string employeeName,
        string toolboxTalkTitle)
    {
        _logger.LogWarning("GenerateCompletionCertificatePdfAsync is not yet implemented. Returning empty PDF stub.");

        // TODO: Implement certificate PDF generation using QuestPDF
        var pdfStub = GenerateStubPdf($"Completion Certificate for {employeeName} - {toolboxTalkTitle}");
        return Task.FromResult(pdfStub);
    }

    /// <summary>
    /// Generate a minimal stub PDF for testing
    /// </summary>
    private static byte[] GenerateStubPdf(string title)
    {
        // This is a minimal valid PDF file
        // In production, use QuestPDF or similar library
        var pdfContent = $@"%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>
endobj
4 0 obj
<< /Length 100 >>
stream
BT
/F1 12 Tf
100 700 Td
({title}) Tj
100 680 Td
(Export functionality coming in Phase 2) Tj
ET
endstream
endobj
5 0 obj
<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>
endobj
xref
0 6
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000266 00000 n
0000000418 00000 n
trailer
<< /Size 6 /Root 1 0 R >>
startxref
495
%%EOF";

        return System.Text.Encoding.ASCII.GetBytes(pdfContent);
    }

    /// <summary>
    /// Generate a minimal stub Excel file for testing
    /// </summary>
    private static byte[] GenerateStubExcel(string sheetName)
    {
        // This returns an empty byte array as a placeholder
        // In production, use ClosedXML to generate proper Excel files
        // For now, return empty bytes - the API will need to handle this gracefully

        // TODO: In Phase 2, implement proper Excel generation:
        // using var workbook = new XLWorkbook();
        // var worksheet = workbook.Worksheets.Add(sheetName);
        // worksheet.Cell("A1").Value = "Export functionality coming in Phase 2";
        // using var stream = new MemoryStream();
        // workbook.SaveAs(stream);
        // return stream.ToArray();

        return Array.Empty<byte>();
    }
}
