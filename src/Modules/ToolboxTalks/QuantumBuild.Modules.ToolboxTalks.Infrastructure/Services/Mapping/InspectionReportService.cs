using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Mapping;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Mapping;

/// <summary>
/// Generates an Inspection Readiness Report PDF from compliance checklist data using QuestPDF,
/// uploads it to R2 storage, and returns the download URL.
/// </summary>
public class InspectionReportService : IInspectionReportService
{
    private readonly IRequirementMappingService _mappingService;
    private readonly ICoreDbContext _coreDbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IR2StorageService _storageService;
    private readonly ILogger<InspectionReportService> _logger;

    // Colour palette — reused from ValidationReportService
    private static readonly string BrandBlue = "#1e3a5f";
    private static readonly string HeaderBg = "#f0f4f8";
    private static readonly string PassGreen = "#16a34a";
    private static readonly string ReviewAmber = "#d97706";
    private static readonly string FailRed = "#dc2626";
    private static readonly string BorderGrey = "#d1d5db";
    private static readonly string LightGrey = "#f9fafb";
    private static readonly string TextDark = "#1f2937";
    private static readonly string TextMuted = "#6b7280";

    public InspectionReportService(
        IRequirementMappingService mappingService,
        ICoreDbContext coreDbContext,
        ICurrentUserService currentUser,
        IR2StorageService storageService,
        ILogger<InspectionReportService> logger)
    {
        _mappingService = mappingService;
        _coreDbContext = coreDbContext;
        _currentUser = currentUser;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<InspectionReportResultDto> GenerateReportAsync(
        string sectorKey,
        GenerateInspectionReportRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating inspection readiness report for sector {SectorKey}", sectorKey);

        // 1. Load compliance checklist data — reuse existing service
        var checklist = await _mappingService.GetComplianceChecklistAsync(sectorKey, cancellationToken);

        // 2. Load tenant details for organisation name
        var tenantId = _currentUser.TenantId;
        var tenant = await _coreDbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        var organisationName = tenant?.CompanyName ?? tenant?.Name ?? "Unknown Organisation";

        // 3. Generate PDF
        var generatedAt = DateTimeOffset.UtcNow;
        var reportDate = generatedAt.ToString("dd MMMM yyyy");
        var pdfBytes = GeneratePdf(checklist, request, organisationName, reportDate, generatedAt);

        // 4. Upload to R2
        var stream = new MemoryStream(pdfBytes);
        stream.Position = 0;
        var uploadResult = await _storageService.UploadInspectionReportAsync(
            tenantId, sectorKey, stream, cancellationToken);
        await stream.DisposeAsync();

        if (!uploadResult.Success)
        {
            _logger.LogError("Failed to upload inspection report: {Error}", uploadResult.ErrorMessage);
            throw new InvalidOperationException($"Failed to store report: {uploadResult.ErrorMessage}");
        }

        _logger.LogInformation("Inspection report uploaded to {Url} ({Size} bytes)",
            uploadResult.PublicUrl, pdfBytes.Length);

        // 5. Return result
        return new InspectionReportResultDto(
            ReportUrl: uploadResult.PublicUrl!,
            GeneratedAt: generatedAt,
            SectorName: checklist.SectorName,
            RegulatoryBody: checklist.RegulatoryBody,
            CoveragePercentage: checklist.CoveragePercentage,
            TotalRequirements: checklist.TotalRequirements,
            CoveredCount: checklist.CoveredCount,
            PendingCount: checklist.PendingCount,
            GapCount: checklist.GapCount
        );
    }

    private byte[] GeneratePdf(
        ComplianceChecklistDto checklist,
        GenerateInspectionReportRequest request,
        string organisationName,
        string reportDate,
        DateTimeOffset generatedAt)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            // =====================
            // Page 1 — Cover Page
            // =====================
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);

                page.Content().Column(col =>
                {
                    col.Item().Height(80);

                    // Branding header
                    col.Item().AlignCenter().Text("CERTIFIEDIQ")
                        .FontSize(14).Bold().FontColor(BrandBlue).LetterSpacing(0.3f);

                    col.Item().Height(6);

                    col.Item().AlignCenter().PaddingHorizontal(180)
                        .LineHorizontal(2).LineColor(BrandBlue);

                    col.Item().Height(50);

                    // Document title
                    col.Item().AlignCenter().Text("INSPECTION READINESS")
                        .FontSize(28).Bold().FontColor(BrandBlue);
                    col.Item().Height(4);
                    col.Item().AlignCenter().Text("REPORT")
                        .FontSize(28).Bold().FontColor(BrandBlue);

                    col.Item().Height(30);

                    // Sector and regulatory body
                    col.Item().AlignCenter().Text($"{checklist.SectorName} — {checklist.RegulatoryBody}")
                        .FontSize(16).SemiBold().FontColor(TextDark);

                    col.Item().Height(8);

                    col.Item().AlignCenter().Text(organisationName)
                        .FontSize(14).FontColor(TextMuted);

                    col.Item().Height(50);

                    // Metadata table
                    col.Item().PaddingHorizontal(60).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(2);
                        });

                        CoverRow(table, "Report Date:", reportDate);
                        CoverRow(table, "Organisation:", organisationName);
                        CoverRow(table, "Responsible Person:", request.ResponsiblePersonName);
                        CoverRow(table, "Role:", request.ResponsiblePersonRole);

                        if (!string.IsNullOrEmpty(request.AuditPurpose))
                        {
                            CoverRow(table, "Purpose:", request.AuditPurpose);
                        }
                    });

                    col.Item().Height(60);

                    col.Item().AlignCenter().PaddingHorizontal(40)
                        .Text("CONFIDENTIAL — This report is intended for internal use and regulatory inspection purposes only.")
                        .FontSize(9).FontColor(TextMuted).Italic();
                });
            });

            // =====================
            // Page 2+ — Executive Summary + Requirement Detail
            // =====================
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("CertifiedIQ Inspection Readiness Report")
                            .FontSize(10).Bold().FontColor(BrandBlue);
                        row.RelativeItem().AlignRight().Text(organisationName)
                            .FontSize(9).FontColor(TextMuted);
                    });
                    col.Item().Height(4);
                    col.Item().LineHorizontal(1).LineColor(BrandBlue);
                    col.Item().Height(10);
                });

                page.Content().Column(col =>
                {
                    // =====================
                    // Executive Summary
                    // =====================
                    col.Item().Text("Executive Summary")
                        .FontSize(16).Bold().FontColor(BrandBlue);
                    col.Item().Height(10);

                    // Overall coverage box
                    col.Item().Border(1).BorderColor(BorderGrey).Padding(15).Column(summary =>
                    {
                        summary.Item().Row(row =>
                        {
                            // Coverage percentage
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Overall Coverage").FontSize(9).FontColor(TextMuted);
                                c.Item().Text($"{checklist.CoveragePercentage}%")
                                    .FontSize(28).Bold().FontColor(CoverageColor(checklist.CoveragePercentage));
                            });

                            // Counts
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Covered").FontSize(9).FontColor(TextMuted);
                                c.Item().Text(checklist.CoveredCount.ToString())
                                    .FontSize(20).Bold().FontColor(PassGreen);
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Pending").FontSize(9).FontColor(TextMuted);
                                c.Item().Text(checklist.PendingCount.ToString())
                                    .FontSize(20).Bold().FontColor(ReviewAmber);
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Gap").FontSize(9).FontColor(TextMuted);
                                c.Item().Text(checklist.GapCount.ToString())
                                    .FontSize(20).Bold().FontColor(FailRed);
                            });
                        });

                        summary.Item().Height(12);

                        // Segmented progress bar
                        summary.Item().Height(10).Row(bar =>
                        {
                            if (checklist.TotalRequirements > 0)
                            {
                                if (checklist.CoveredCount > 0)
                                    bar.RelativeItem(checklist.CoveredCount).Background(PassGreen);
                                if (checklist.PendingCount > 0)
                                    bar.RelativeItem(checklist.PendingCount).Background(ReviewAmber);
                                if (checklist.GapCount > 0)
                                    bar.RelativeItem(checklist.GapCount).Background("#e5e7eb");
                            }
                            else
                            {
                                bar.RelativeItem(1).Background("#e5e7eb");
                            }
                        });
                    });

                    col.Item().Height(20);

                    // Per-principle summary table
                    col.Item().Text("Coverage by Principle")
                        .FontSize(13).Bold().FontColor(BrandBlue);
                    col.Item().Height(8);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(4); // Principle
                            cols.RelativeColumn(1); // Total
                            cols.RelativeColumn(1); // Covered
                            cols.RelativeColumn(1); // Pending
                            cols.RelativeColumn(1); // Gap
                            cols.RelativeColumn(1); // Coverage %
                        });

                        // Header row
                        TableHeader(table, "Principle");
                        TableHeader(table, "Total");
                        TableHeader(table, "Covered");
                        TableHeader(table, "Pending");
                        TableHeader(table, "Gap");
                        TableHeader(table, "Coverage %");

                        foreach (var group in checklist.PrincipleGroups)
                        {
                            var principleLabel = !string.IsNullOrEmpty(group.Principle)
                                ? $"{group.Principle}{(!string.IsNullOrEmpty(group.PrincipleLabel) ? $" — {group.PrincipleLabel}" : "")}"
                                : "Uncategorised";
                            var groupCoverage = group.TotalRequirements > 0
                                ? (int)Math.Round(100.0 * group.CoveredCount / group.TotalRequirements)
                                : 0;

                            TableCell(table, principleLabel);
                            TableCellCentered(table, group.TotalRequirements.ToString());
                            TableCellCentered(table, group.CoveredCount.ToString(), PassGreen);
                            TableCellCentered(table, group.PendingCount.ToString(), ReviewAmber);
                            TableCellCentered(table, group.GapCount.ToString(), FailRed);
                            TableCellCentered(table, $"{groupCoverage}%", CoverageColor(groupCoverage));
                        }

                        // Total row
                        table.Cell().Background(HeaderBg).Border(1).BorderColor(BorderGrey).Padding(5)
                            .Text("Total").FontSize(9).Bold().FontColor(TextDark);
                        TableCellCentered(table, checklist.TotalRequirements.ToString(), TextDark, true);
                        TableCellCentered(table, checklist.CoveredCount.ToString(), PassGreen, true);
                        TableCellCentered(table, checklist.PendingCount.ToString(), ReviewAmber, true);
                        TableCellCentered(table, checklist.GapCount.ToString(), FailRed, true);
                        TableCellCentered(table, $"{checklist.CoveragePercentage}%",
                            CoverageColor(checklist.CoveragePercentage), true);
                    });

                    col.Item().Height(20);

                    // =====================
                    // Requirement Detail — grouped by principle
                    // =====================
                    col.Item().Text("Requirement Detail")
                        .FontSize(16).Bold().FontColor(BrandBlue);
                    col.Item().Height(10);

                    foreach (var group in checklist.PrincipleGroups)
                    {
                        col.Item().PageBreak();

                        // Principle section header
                        var principleTitle = !string.IsNullOrEmpty(group.Principle)
                            ? $"{group.Principle}{(!string.IsNullOrEmpty(group.PrincipleLabel) ? $" — {group.PrincipleLabel}" : "")}"
                            : "Uncategorised";

                        col.Item().Background(BrandBlue).Padding(10)
                            .Text(principleTitle).FontSize(13).Bold().FontColor(Colors.White);

                        col.Item().Height(8);

                        foreach (var req in group.Requirements)
                        {
                            col.Item().EnsureSpace(80).Column(reqCol =>
                            {
                                // Requirement header row
                                reqCol.Item().Border(1).BorderColor(BorderGrey).Background(HeaderBg)
                                    .Padding(8).Row(row =>
                                    {
                                        row.RelativeItem().Column(c =>
                                        {
                                            c.Item().Row(titleRow =>
                                            {
                                                titleRow.AutoItem().Text(req.Title)
                                                    .FontSize(10).Bold().FontColor(TextDark);
                                                titleRow.AutoItem().PaddingLeft(6)
                                                    .Text(req.Priority.ToUpper())
                                                    .FontSize(8).Bold().FontColor(PriorityColor(req.Priority));
                                                if (!string.IsNullOrEmpty(req.Section))
                                                {
                                                    titleRow.AutoItem().PaddingLeft(6)
                                                        .Text(req.Section)
                                                        .FontSize(8).FontColor(TextMuted);
                                                }
                                            });
                                        });

                                        row.ConstantItem(100).AlignRight()
                                            .Text(StatusLabel(req.CoverageStatus))
                                            .FontSize(9).Bold().FontColor(StatusColor(req.CoverageStatus));
                                    });

                                // Description
                                reqCol.Item().Border(1).BorderColor(BorderGrey).Padding(8).Column(content =>
                                {
                                    content.Item().Text(req.Description)
                                        .FontSize(9).FontColor(TextMuted);

                                    content.Item().Height(6);

                                    // Evidence / status detail
                                    if (req.CoverageStatus == "Covered")
                                    {
                                        var mapping = req.Mappings.FirstOrDefault(m =>
                                            m.MappingStatus == "Confirmed" && m.ValidationOutcome != null);
                                        if (mapping != null)
                                        {
                                            content.Item().Background(LightGrey).Padding(6).Column(evidence =>
                                            {
                                                evidence.Item().Text($"Training Content: {mapping.ContentTitle} ({mapping.ContentType})")
                                                    .FontSize(9).FontColor(TextDark);
                                                if (mapping.ValidationScore.HasValue)
                                                {
                                                    evidence.Item().Height(2);
                                                    evidence.Item().Text($"Validation Score: {mapping.ValidationScore}% ({mapping.ValidationOutcome})")
                                                        .FontSize(9).FontColor(PassGreen);
                                                }
                                                if (mapping.ValidationDate.HasValue)
                                                {
                                                    evidence.Item().Height(2);
                                                    var valDate = DateTimeOffset.Parse(mapping.ValidationDate.Value.ToString("o"));
                                                    evidence.Item().Text($"Validated: {valDate:dd MMM yyyy}")
                                                        .FontSize(8).FontColor(TextMuted);
                                                }
                                            });
                                        }
                                    }
                                    else if (req.CoverageStatus == "Pending")
                                    {
                                        content.Item().Background("#fef3c7").Padding(6)
                                            .Text("Mapping suggested — pending review")
                                            .FontSize(9).FontColor(ReviewAmber);
                                    }
                                    else // Gap
                                    {
                                        content.Item().Background("#fef2f2").Padding(6)
                                            .Text("No training content mapped to this requirement")
                                            .FontSize(9).FontColor(FailRed);
                                    }
                                });

                                reqCol.Item().Height(8);
                            });
                        }
                    }

                    // =====================
                    // Declaration and Disclaimer
                    // =====================
                    col.Item().PageBreak();

                    col.Item().Text("Declaration")
                        .FontSize(16).Bold().FontColor(BrandBlue);
                    col.Item().Height(12);

                    col.Item().Text(text =>
                    {
                        text.Span($"I, {request.ResponsiblePersonName}, {request.ResponsiblePersonRole}, confirm that this report accurately represents the organisation's training programme coverage against {checklist.RegulatoryBody} regulatory requirements as of {reportDate}.")
                            .FontSize(11).FontColor(TextDark);
                    });

                    col.Item().Height(40);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Signed: _________________________________")
                                .FontSize(10).FontColor(TextDark);
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Date: _____________")
                                .FontSize(10).FontColor(TextDark);
                        });
                    });

                    col.Item().Height(40);

                    // Disclaimer box
                    col.Item().Border(1).BorderColor(BorderGrey).Background(LightGrey).Padding(12).Column(disclaimer =>
                    {
                        disclaimer.Item().Text("DISCLAIMER")
                            .FontSize(10).Bold().FontColor(TextMuted);
                        disclaimer.Item().Height(6);
                        disclaimer.Item().Text(
                            "This report is generated from training content and AI-assisted requirement mappings within CertifiedIQ. " +
                            "It is the organisation's responsibility to ensure all regulatory requirements are met and that mappings accurately reflect " +
                            "their training programme. CertifiedIQ does not provide legal or regulatory advice. " +
                            "This report does not constitute legal compliance certification.")
                            .FontSize(9).FontColor(TextMuted);
                    });
                });

                page.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor(BorderGrey);
                    col.Item().Height(4);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"CertifiedIQ Inspection Readiness Report | {organisationName} | {checklist.SectorName}")
                            .FontSize(7).FontColor(TextMuted);
                        row.RelativeItem().AlignCenter().Text(text =>
                        {
                            text.Span("Page ").FontSize(7).FontColor(TextMuted);
                            text.CurrentPageNumber().FontSize(7).FontColor(TextMuted);
                            text.Span(" of ").FontSize(7).FontColor(TextMuted);
                            text.TotalPages().FontSize(7).FontColor(TextMuted);
                        });
                        row.RelativeItem().AlignRight().Text($"Generated: {generatedAt:yyyy-MM-dd HH:mm} UTC")
                            .FontSize(7).FontColor(TextMuted);
                    });
                });
            });
        });

        using var pdfStream = new MemoryStream();
        document.GeneratePdf(pdfStream);
        return pdfStream.ToArray();
    }

    #region QuestPDF Helpers

    private static void CoverRow(TableDescriptor table, string label, string value)
    {
        table.Cell().PaddingVertical(4).Text(label)
            .FontSize(11).Bold().FontColor(TextMuted);
        table.Cell().PaddingVertical(4).Text(value)
            .FontSize(11).FontColor(TextDark);
    }

    private static void TableHeader(TableDescriptor table, string text)
    {
        table.Cell().Background(BrandBlue).Padding(5)
            .Text(text).FontSize(8).Bold().FontColor(Colors.White);
    }

    private static void TableCell(TableDescriptor table, string text)
    {
        table.Cell().Border(1).BorderColor(BorderGrey).Padding(5)
            .Text(text).FontSize(8).FontColor(TextDark);
    }

    private static void TableCellCentered(TableDescriptor table, string text, string? color = null, bool bold = false)
    {
        var cell = table.Cell().Border(1).BorderColor(BorderGrey).Padding(5)
            .AlignCenter().Text(text).FontSize(8).FontColor(color ?? TextDark);
        if (bold) cell.Bold();
    }

    private static string CoverageColor(int percentage) => percentage switch
    {
        >= 70 => PassGreen,
        >= 40 => ReviewAmber,
        _ => FailRed
    };

    private static string StatusColor(string status) => status switch
    {
        "Covered" => PassGreen,
        "Pending" => ReviewAmber,
        "Gap" => FailRed,
        _ => TextMuted
    };

    private static string StatusLabel(string status) => status switch
    {
        "Covered" => "\u2713 COVERED",
        "Pending" => "\u26A0 PENDING REVIEW",
        "Gap" => "\u2717 GAP",
        _ => status.ToUpper()
    };

    private static string PriorityColor(string priority) => priority.ToLower() switch
    {
        "high" => FailRed,
        "med" => ReviewAmber,
        "low" => TextMuted,
        _ => TextMuted
    };

    #endregion
}
