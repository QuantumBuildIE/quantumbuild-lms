using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

/// <summary>
/// Generates a formal PDF audit report for a completed translation validation run using QuestPDF.
/// </summary>
public class ValidationReportService(ILogger<ValidationReportService> logger) : IValidationReportService
{
    // Colour palette
    private static readonly string BrandBlue = "#1e3a5f";
    private static readonly string HeaderBg = "#f0f4f8";
    private static readonly string PassGreen = "#16a34a";
    private static readonly string ReviewAmber = "#d97706";
    private static readonly string FailRed = "#dc2626";
    private static readonly string BorderGrey = "#d1d5db";
    private static readonly string LightGrey = "#f9fafb";
    private static readonly string TextDark = "#1f2937";
    private static readonly string TextMuted = "#6b7280";

    public Task<byte[]> GenerateAsync(TranslationValidationRun run, CancellationToken ct = default)
    {
        logger.LogInformation("Generating validation report PDF for run {RunId}", run.Id);

        QuestPDF.Settings.License = LicenseType.Community;

        var talkTitle = run.ToolboxTalk?.Title ?? "Unknown Talk";
        var generatedAt = DateTime.UtcNow;
        var results = run.Results.OrderBy(r => r.SectionIndex).ToList();

        var document = Document.Create(container =>
        {
            // Cover page
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);

                page.Content().Column(col =>
                {
                    col.Item().Height(80);

                    // Branding header
                    col.Item().AlignCenter().Text("QUANTUMBUILD LMS")
                        .FontSize(14).Bold().FontColor(BrandBlue).LetterSpacing(0.3f);

                    col.Item().Height(6);

                    col.Item().AlignCenter().PaddingHorizontal(180)
                        .LineHorizontal(2).LineColor(BrandBlue);

                    col.Item().Height(50);

                    // Document title
                    col.Item().AlignCenter().Text("TRANSLATION VALIDATION")
                        .FontSize(28).Bold().FontColor(BrandBlue);
                    col.Item().Height(4);
                    col.Item().AlignCenter().Text("AUDIT REPORT")
                        .FontSize(28).Bold().FontColor(BrandBlue);

                    col.Item().Height(40);

                    // Talk title
                    col.Item().AlignCenter().Text(talkTitle)
                        .FontSize(18).SemiBold().FontColor(TextDark);

                    col.Item().Height(8);

                    col.Item().AlignCenter().Text($"Language: {run.LanguageCode.ToUpper()}")
                        .FontSize(13).FontColor(TextMuted);

                    if (!string.IsNullOrEmpty(run.SectorKey))
                    {
                        col.Item().Height(4);
                        col.Item().AlignCenter().Text($"Sector: {run.SectorKey}")
                            .FontSize(13).FontColor(TextMuted);
                    }

                    col.Item().Height(50);

                    // Metadata table
                    col.Item().PaddingHorizontal(60).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(2);
                        });

                        CoverRow(table, "Document Ref:", run.DocumentRef ?? $"VR-{run.Id.ToString("N")[..8].ToUpper()}");
                        CoverRow(table, "Date Generated:", generatedAt.ToString("dd MMMM yyyy"));
                        CoverRow(table, "Reviewer:", run.ReviewerName ?? "—");
                        CoverRow(table, "Organisation:", run.ReviewerOrg ?? "—");
                        CoverRow(table, "Role:", run.ReviewerRole ?? "—");
                        CoverRow(table, "Client:", run.ClientName ?? "—");
                        CoverRow(table, "Purpose:", run.AuditPurpose ?? "Translation quality audit");
                    });

                    col.Item().Height(40);

                    col.Item().AlignCenter().Text("CONFIDENTIAL")
                        .FontSize(10).Bold().FontColor(TextMuted).LetterSpacing(0.2f);
                });
            });

            // Executive summary + section details
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Translation Validation Audit Report")
                            .FontSize(10).Bold().FontColor(BrandBlue);
                        row.RelativeItem().AlignRight().Text(run.DocumentRef ?? $"VR-{run.Id.ToString("N")[..8].ToUpper()}")
                            .FontSize(9).FontColor(TextMuted);
                    });
                    col.Item().Height(4);
                    col.Item().LineHorizontal(1).LineColor(BrandBlue);
                    col.Item().Height(10);
                });

                page.Content().Column(col =>
                {
                    // Executive Summary
                    col.Item().Text("1. Executive Summary")
                        .FontSize(16).Bold().FontColor(BrandBlue);
                    col.Item().Height(10);

                    col.Item().Border(1).BorderColor(BorderGrey).Padding(15).Column(summary =>
                    {
                        summary.Item().Row(row =>
                        {
                            // Overall score
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Overall Score").FontSize(9).FontColor(TextMuted);
                                c.Item().Text($"{run.OverallScore}%")
                                    .FontSize(28).Bold().FontColor(OutcomeColor(run.OverallOutcome));
                            });

                            // Outcome
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Outcome").FontSize(9).FontColor(TextMuted);
                                c.Item().Text(run.OverallOutcome.ToString().ToUpper())
                                    .FontSize(16).Bold().FontColor(OutcomeColor(run.OverallOutcome));
                            });

                            // Safety verdict
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Safety Verdict").FontSize(9).FontColor(TextMuted);
                                c.Item().Text(run.SafetyVerdict?.ToString().ToUpper() ?? "N/A")
                                    .FontSize(16).Bold().FontColor(run.SafetyVerdict.HasValue
                                        ? OutcomeColor(run.SafetyVerdict.Value) : TextMuted);
                            });
                        });

                        summary.Item().Height(12);

                        summary.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Pass Threshold").FontSize(9).FontColor(TextMuted);
                                c.Item().Text($"{run.PassThreshold}%").FontSize(13).FontColor(TextDark);
                            });
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Source Language").FontSize(9).FontColor(TextMuted);
                                c.Item().Text(run.SourceLanguage).FontSize(13).FontColor(TextDark);
                            });
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Target Language").FontSize(9).FontColor(TextMuted);
                                c.Item().Text(run.LanguageCode.ToUpper()).FontSize(13).FontColor(TextDark);
                            });
                        });

                        summary.Item().Height(12);

                        // Section counts
                        summary.Item().Table(st =>
                        {
                            st.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn();
                                cols.RelativeColumn();
                                cols.RelativeColumn();
                                cols.RelativeColumn();
                            });

                            SummaryCountCell(st, "Total Sections", run.TotalSections.ToString(), TextDark);
                            SummaryCountCell(st, "Passed", run.PassedSections.ToString(), PassGreen);
                            SummaryCountCell(st, "Review", run.ReviewSections.ToString(), ReviewAmber);
                            SummaryCountCell(st, "Failed", run.FailedSections.ToString(), FailRed);
                        });
                    });

                    col.Item().Height(20);

                    // Section details
                    col.Item().Text("2. Section Detail")
                        .FontSize(16).Bold().FontColor(BrandBlue);
                    col.Item().Height(10);

                    foreach (var result in results)
                    {
                        col.Item().EnsureSpace(100).Column(section =>
                        {
                            // Section header
                            section.Item().Background(HeaderBg).Border(1).BorderColor(BorderGrey)
                                .Padding(10).Row(row =>
                                {
                                    row.RelativeItem().Text($"Section {result.SectionIndex + 1}: {result.SectionTitle}")
                                        .FontSize(11).Bold().FontColor(TextDark);
                                    row.ConstantItem(70).AlignRight().Text(result.Outcome.ToString().ToUpper())
                                        .FontSize(10).Bold().FontColor(OutcomeColor(result.Outcome));
                                });

                            // Content
                            section.Item().Border(1).BorderColor(BorderGrey).Padding(10).Column(content =>
                            {
                                // Original text
                                content.Item().Text("Original Text").FontSize(9).Bold().FontColor(TextMuted);
                                content.Item().Height(3);
                                content.Item().Background(LightGrey).Padding(8)
                                    .Text(TruncateText(result.OriginalText, 500))
                                    .FontSize(9).FontColor(TextDark);

                                content.Item().Height(8);

                                // Translated text
                                content.Item().Text("Translated Text").FontSize(9).Bold().FontColor(TextMuted);
                                content.Item().Height(3);
                                content.Item().Background(LightGrey).Padding(8)
                                    .Text(TruncateText(result.TranslatedText, 500))
                                    .FontSize(9).FontColor(TextDark);

                                // Edited translation if present
                                if (!string.IsNullOrEmpty(result.EditedTranslation))
                                {
                                    content.Item().Height(8);
                                    content.Item().Text("Edited Translation").FontSize(9).Bold().FontColor(ReviewAmber);
                                    content.Item().Height(3);
                                    content.Item().Background("#fef3c7").Padding(8)
                                        .Text(TruncateText(result.EditedTranslation, 500))
                                        .FontSize(9).FontColor(TextDark);
                                }

                                content.Item().Height(10);

                                // Scores table
                                content.Item().Table(st =>
                                {
                                    st.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn();
                                        cols.RelativeColumn();
                                        cols.RelativeColumn();
                                        cols.RelativeColumn();
                                        cols.RelativeColumn();
                                    });

                                    // Header
                                    ScoreHeader(st, "Score A");
                                    ScoreHeader(st, "Score B");
                                    ScoreHeader(st, "Score C");
                                    ScoreHeader(st, "Score D");
                                    ScoreHeader(st, "Consensus");

                                    // Values
                                    ScoreCell(st, $"{result.ScoreA}%");
                                    ScoreCell(st, $"{result.ScoreB}%");
                                    ScoreCell(st, result.ScoreC.HasValue ? $"{result.ScoreC}%" : "—");
                                    ScoreCell(st, result.ScoreD.HasValue ? $"{result.ScoreD}%" : "—");
                                    ScoreCell(st, $"{result.FinalScore}%", true);
                                });

                                content.Item().Height(6);

                                // Metadata row
                                content.Item().Row(row =>
                                {
                                    row.RelativeItem().Text($"Rounds used: {result.RoundsUsed}")
                                        .FontSize(8).FontColor(TextMuted);
                                    row.RelativeItem().Text($"Engine: {result.EngineOutcome}")
                                        .FontSize(8).FontColor(TextMuted);
                                    row.RelativeItem().Text($"Threshold: {result.EffectiveThreshold}%")
                                        .FontSize(8).FontColor(TextMuted);
                                    row.RelativeItem().AlignRight()
                                        .Text($"Reviewer: {FormatDecision(result.ReviewerDecision)}")
                                        .FontSize(8).FontColor(TextMuted);
                                });

                                // Safety flags
                                if (result.IsSafetyCritical)
                                {
                                    content.Item().Height(6);
                                    content.Item().Background("#fef2f2").Padding(6).Column(safety =>
                                    {
                                        safety.Item().Text("SAFETY-CRITICAL SECTION")
                                            .FontSize(9).Bold().FontColor(FailRed);
                                        if (!string.IsNullOrEmpty(result.CriticalTerms))
                                        {
                                            safety.Item().Height(2);
                                            safety.Item().Text($"Critical terms: {result.CriticalTerms}")
                                                .FontSize(8).FontColor(TextDark);
                                        }
                                    });
                                }

                                // Glossary mismatches
                                if (!string.IsNullOrEmpty(result.GlossaryMismatches))
                                {
                                    content.Item().Height(6);
                                    content.Item().Background("#fff7ed").Padding(6).Column(glossary =>
                                    {
                                        glossary.Item().Text("Glossary Mismatches")
                                            .FontSize(9).Bold().FontColor(ReviewAmber);
                                        glossary.Item().Height(2);
                                        glossary.Item().Text(result.GlossaryMismatches)
                                            .FontSize(8).FontColor(TextDark);
                                    });
                                }
                            });

                            section.Item().Height(12);
                        });
                    }

                    // Safety glossary section
                    var safetySections = results.Where(r => r.IsSafetyCritical).ToList();
                    if (safetySections.Count > 0)
                    {
                        col.Item().Height(10);
                        col.Item().Text("3. Safety Glossary Verification")
                            .FontSize(16).Bold().FontColor(BrandBlue);
                        col.Item().Height(10);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(4);
                                cols.RelativeColumn(4);
                                cols.RelativeColumn(2);
                            });

                            // Header row
                            GlossaryHeader(table, "#");
                            GlossaryHeader(table, "Section");
                            GlossaryHeader(table, "Critical Terms");
                            GlossaryHeader(table, "Status");

                            foreach (var s in safetySections)
                            {
                                var statusText = s.Outcome == ValidationOutcome.Pass ? "Verified" : "Flagged";
                                var statusColor = s.Outcome == ValidationOutcome.Pass ? PassGreen : FailRed;

                                GlossaryCell(table, (s.SectionIndex + 1).ToString());
                                GlossaryCell(table, TruncateText(s.SectionTitle, 40));
                                GlossaryCell(table, s.CriticalTerms ?? "—");
                                table.Cell().Border(1).BorderColor(BorderGrey).Padding(5)
                                    .Text(statusText).FontSize(8).Bold().FontColor(statusColor);
                            }
                        });
                    }
                });

                page.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor(BorderGrey);
                    col.Item().Height(4);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Ref: {run.DocumentRef ?? $"VR-{run.Id.ToString("N")[..8].ToUpper()}"}")
                            .FontSize(8).FontColor(TextMuted);
                        row.RelativeItem().AlignCenter().Text(text =>
                        {
                            text.Span("Page ").FontSize(8).FontColor(TextMuted);
                            text.CurrentPageNumber().FontSize(8).FontColor(TextMuted);
                            text.Span(" of ").FontSize(8).FontColor(TextMuted);
                            text.TotalPages().FontSize(8).FontColor(TextMuted);
                        });
                        row.RelativeItem().AlignRight().Text($"Generated: {generatedAt:yyyy-MM-dd HH:mm} UTC")
                            .FontSize(8).FontColor(TextMuted);
                    });
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        var bytes = stream.ToArray();

        logger.LogInformation("Generated validation report PDF for run {RunId} ({Size} bytes)", run.Id, bytes.Length);

        return Task.FromResult(bytes);
    }

    #region QuestPDF Helpers

    private static void CoverRow(TableDescriptor table, string label, string value)
    {
        table.Cell().PaddingVertical(4).Text(label)
            .FontSize(11).Bold().FontColor(TextMuted);
        table.Cell().PaddingVertical(4).Text(value)
            .FontSize(11).FontColor(TextDark);
    }

    private static void SummaryCountCell(TableDescriptor table, string label, string value, string color)
    {
        table.Cell().Padding(8).AlignCenter().Column(c =>
        {
            c.Item().AlignCenter().Text(label).FontSize(9).FontColor(TextMuted);
            c.Item().AlignCenter().Text(value).FontSize(20).Bold().FontColor(color);
        });
    }

    private static void ScoreHeader(TableDescriptor table, string text)
    {
        table.Cell().Background(HeaderBg).Border(1).BorderColor(BorderGrey)
            .Padding(4).AlignCenter().Text(text)
            .FontSize(8).Bold().FontColor(TextMuted);
    }

    private static void ScoreCell(TableDescriptor table, string text, bool bold = false)
    {
        var cell = table.Cell().Border(1).BorderColor(BorderGrey)
            .Padding(4).AlignCenter().Text(text).FontSize(9).FontColor(TextDark);
        if (bold) cell.Bold();
    }

    private static void GlossaryHeader(TableDescriptor table, string text)
    {
        table.Cell().Background(BrandBlue).Padding(5)
            .Text(text).FontSize(8).Bold().FontColor(Colors.White);
    }

    private static void GlossaryCell(TableDescriptor table, string text)
    {
        table.Cell().Border(1).BorderColor(BorderGrey).Padding(5)
            .Text(text).FontSize(8).FontColor(TextDark);
    }

    private static string OutcomeColor(ValidationOutcome outcome) => outcome switch
    {
        ValidationOutcome.Pass => PassGreen,
        ValidationOutcome.Review => ReviewAmber,
        ValidationOutcome.Fail => FailRed,
        _ => TextMuted
    };

    private static string FormatDecision(ReviewerDecision decision) => decision switch
    {
        ReviewerDecision.Accepted => "Accepted",
        ReviewerDecision.Rejected => "Rejected",
        ReviewerDecision.Edited => "Edited",
        _ => "Pending"
    };

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "—";
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }

    #endregion
}
