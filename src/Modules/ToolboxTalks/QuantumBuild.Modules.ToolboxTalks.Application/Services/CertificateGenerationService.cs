using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuantumBuild.Core.Application.Features.TenantSettings;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Services;

public class CertificateGenerationService(
    IToolboxTalksDbContext context,
    IR2StorageService storageService,
    ITenantSettingsService tenantSettingsService,
    ILogger<CertificateGenerationService> logger) : ICertificateGenerationService
{
    public async Task<ToolboxTalkCertificate?> GenerateTalkCertificateAsync(
        ScheduledTalk completedTalk,
        string? signatureDataUrl,
        CancellationToken ct = default)
    {
        // Skip if talk is part of a course — course certificates handle that
        if (completedTalk.CourseAssignmentId.HasValue)
        {
            logger.LogDebug("Skipping talk certificate for ScheduledTalk {Id} — part of course assignment {CourseAssignmentId}",
                completedTalk.Id, completedTalk.CourseAssignmentId);
            return null;
        }

        // Load the toolbox talk
        var talk = await context.ToolboxTalks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == completedTalk.ToolboxTalkId && !t.IsDeleted, ct);

        if (talk == null)
        {
            logger.LogWarning("ToolboxTalk {TalkId} not found for certificate generation", completedTalk.ToolboxTalkId);
            return null;
        }

        if (!talk.GenerateCertificate)
        {
            logger.LogDebug("ToolboxTalk {TalkId} does not require certificate generation", talk.Id);
            return null;
        }

        // Load employee
        var employee = await context.ScheduledTalks
            .IgnoreQueryFilters()
            .Where(st => st.Id == completedTalk.Id)
            .Select(st => st.Employee)
            .FirstOrDefaultAsync(ct);

        if (employee == null)
        {
            logger.LogWarning("Employee {EmployeeId} not found for certificate generation", completedTalk.EmployeeId);
            return null;
        }

        var now = DateTime.UtcNow;
        var talkPrefix = await tenantSettingsService.GetSettingAsync(
            completedTalk.TenantId, TenantSettingKeys.TalkCertificatePrefix, TenantSettingKeys.Defaults.TalkCertificatePrefix, ct);
        var certificateNumber = await GenerateCertificateNumber(talkPrefix, completedTalk.TenantId, ct);

        DateTime? expiresAt = talk.RequiresRefresher
            ? now.AddMonths(talk.RefresherIntervalMonths)
            : null;

        var certificate = new ToolboxTalkCertificate
        {
            Id = Guid.NewGuid(),
            TenantId = completedTalk.TenantId,
            EmployeeId = completedTalk.EmployeeId,
            CertificateType = CertificateType.Talk,
            ToolboxTalkId = talk.Id,
            ScheduledTalkId = completedTalk.Id,
            CertificateNumber = certificateNumber,
            IssuedAt = now,
            ExpiresAt = expiresAt,
            IsRefresher = completedTalk.IsRefresher,
            EmployeeName = employee.FullName,
            EmployeeCode = employee.EmployeeCode,
            TrainingTitle = talk.Title,
            SignatureDataUrl = signatureDataUrl
        };

        // Generate PDF and upload
        var pdfBytes = GenerateCertificatePdf(certificate, null);
        var storagePath = await UploadCertificatePdf(certificate, pdfBytes, ct);

        if (storagePath == null)
        {
            logger.LogError("Failed to upload certificate PDF for ScheduledTalk {Id}", completedTalk.Id);
            return null;
        }

        certificate.PdfStoragePath = storagePath;

        context.ToolboxTalkCertificates.Add(certificate);
        var saved = await context.SaveChangesAsync(ct);
        logger.LogInformation("Saved talk certificate {CertificateNumber} for employee {EmployeeName} ({Rows} rows)",
            certificateNumber, employee.FullName, saved);

        return certificate;
    }

    public async Task<ToolboxTalkCertificate?> GenerateCourseCertificateAsync(
        ToolboxTalkCourseAssignment completedAssignment,
        string? signatureDataUrl,
        CancellationToken ct = default)
    {
        // Load course with items and talks
        var course = await context.ToolboxTalkCourses
            .IgnoreQueryFilters()
            .Include(c => c.CourseItems.Where(ci => !ci.IsDeleted))
                .ThenInclude(ci => ci.ToolboxTalk)
            .FirstOrDefaultAsync(c => c.Id == completedAssignment.CourseId && !c.IsDeleted, ct);

        if (course == null)
        {
            logger.LogWarning("Course {CourseId} not found for certificate generation", completedAssignment.CourseId);
            return null;
        }

        if (!course.GenerateCertificate)
        {
            logger.LogDebug("Course {CourseId} does not require certificate generation", course.Id);
            return null;
        }

        // Load employee
        var employee = await context.ToolboxTalkCourseAssignments
            .IgnoreQueryFilters()
            .Where(a => a.Id == completedAssignment.Id)
            .Select(a => a.Employee)
            .FirstOrDefaultAsync(ct);

        if (employee == null)
        {
            logger.LogWarning("Employee {EmployeeId} not found for certificate generation", completedAssignment.EmployeeId);
            return null;
        }

        var includedTalks = course.CourseItems
            .OrderBy(ci => ci.OrderIndex)
            .Select(ci => ci.ToolboxTalk.Title)
            .ToList();

        var now = DateTime.UtcNow;
        var coursePrefix = await tenantSettingsService.GetSettingAsync(
            completedAssignment.TenantId, TenantSettingKeys.CourseCertificatePrefix, TenantSettingKeys.Defaults.CourseCertificatePrefix, ct);
        var certificateNumber = await GenerateCertificateNumber(coursePrefix, completedAssignment.TenantId, ct);

        DateTime? expiresAt = course.RequiresRefresher
            ? now.AddMonths(course.RefresherIntervalMonths)
            : null;

        var certificate = new ToolboxTalkCertificate
        {
            Id = Guid.NewGuid(),
            TenantId = completedAssignment.TenantId,
            EmployeeId = completedAssignment.EmployeeId,
            CertificateType = CertificateType.Course,
            CourseId = course.Id,
            CourseAssignmentId = completedAssignment.Id,
            CertificateNumber = certificateNumber,
            IssuedAt = now,
            ExpiresAt = expiresAt,
            IsRefresher = completedAssignment.IsRefresher,
            EmployeeName = employee.FullName,
            EmployeeCode = employee.EmployeeCode,
            TrainingTitle = course.Title,
            IncludedTalksJson = JsonSerializer.Serialize(includedTalks),
            SignatureDataUrl = signatureDataUrl
        };

        // Generate PDF and upload
        var pdfBytes = GenerateCertificatePdf(certificate, includedTalks);
        var storagePath = await UploadCertificatePdf(certificate, pdfBytes, ct);

        if (storagePath == null)
        {
            logger.LogError("Failed to upload certificate PDF for CourseAssignment {Id}", completedAssignment.Id);
            return null;
        }

        certificate.PdfStoragePath = storagePath;

        context.ToolboxTalkCertificates.Add(certificate);
        var saved = await context.SaveChangesAsync(ct);
        logger.LogInformation("Saved course certificate {CertificateNumber} for employee {EmployeeName} ({Rows} rows)",
            certificateNumber, employee.FullName, saved);

        return certificate;
    }

    private async Task<string> GenerateCertificateNumber(string prefix, Guid tenantId, CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var pattern = $"{prefix}-{year}-";
        var count = await context.ToolboxTalkCertificates
            .IgnoreQueryFilters()
            .CountAsync(c => c.TenantId == tenantId && c.CertificateNumber.StartsWith(pattern), ct);
        return $"{prefix}-{year}-{(count + 1):D6}";
    }

    private async Task<string?> UploadCertificatePdf(ToolboxTalkCertificate cert, byte[] pdfBytes, CancellationToken ct)
    {
        using var stream = new MemoryStream(pdfBytes);
        var result = await storageService.UploadCertificateAsync(cert.TenantId, cert.CertificateNumber, stream, ct);

        if (!result.Success)
        {
            logger.LogError("Certificate PDF upload failed for {CertificateNumber}: {Error}",
                cert.CertificateNumber, result.ErrorMessage);
            return null;
        }

        return result.PublicUrl;
    }

    private static byte[] GenerateCertificatePdf(ToolboxTalkCertificate cert, List<string>? includedTalks)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);

                page.Content()
                    .Border(3).BorderColor(Colors.Grey.Darken3).Padding(8)
                    .Border(1).BorderColor(Colors.Grey.Darken1).Padding(25)
                    .Column(column =>
                    {
                        // Decorative header
                        column.Item().AlignCenter().Text("\u2726 \u2726 \u2726")
                            .FontSize(16).FontColor(Colors.Grey.Darken2);
                        column.Item().Height(15);

                        // Title
                        column.Item().AlignCenter().Text("CERTIFICATE OF COMPLETION")
                            .FontSize(32).Bold().FontColor(Colors.Grey.Darken3)
                            .LetterSpacing(0.1f);

                        column.Item().Height(8);
                        column.Item().AlignCenter().PaddingHorizontal(150)
                            .LineHorizontal(2).LineColor(Colors.Grey.Darken2);
                        column.Item().Height(25);

                        // Certification text
                        column.Item().AlignCenter().Text("This is to certify that")
                            .FontSize(14).Italic().FontColor(Colors.Grey.Darken2);
                        column.Item().Height(15);

                        // Employee name
                        column.Item().AlignCenter().Text(cert.EmployeeName)
                            .FontSize(28).Bold().FontColor(Colors.Grey.Darken4);

                        if (!string.IsNullOrEmpty(cert.EmployeeCode))
                        {
                            column.Item().Height(5);
                            column.Item().AlignCenter().Text($"Employee ID: {cert.EmployeeCode}")
                                .FontSize(11).FontColor(Colors.Grey.Darken1);
                        }

                        column.Item().Height(20);
                        column.Item().AlignCenter().Text("has successfully completed the training")
                            .FontSize(14).Italic().FontColor(Colors.Grey.Darken2);
                        column.Item().Height(15);

                        // Training title
                        column.Item().AlignCenter().Text(cert.TrainingTitle)
                            .FontSize(22).Bold().FontColor(Colors.Grey.Darken4);

                        // Included talks for course certificates
                        if (includedTalks is { Count: > 0 })
                        {
                            column.Item().Height(15);
                            column.Item().AlignCenter().Text("Including:")
                                .FontSize(11).Italic().FontColor(Colors.Grey.Darken2);
                            column.Item().Height(8);

                            foreach (var talk in includedTalks)
                            {
                                column.Item().AlignCenter().Text($"\u2022 {talk}")
                                    .FontSize(10).FontColor(Colors.Grey.Darken2);
                            }
                        }

                        column.Item().Height(25);

                        // Signature
                        if (!string.IsNullOrEmpty(cert.SignatureDataUrl))
                        {
                            try
                            {
                                var base64Data = cert.SignatureDataUrl.Contains(',')
                                    ? cert.SignatureDataUrl.Split(',').Last()
                                    : cert.SignatureDataUrl;
                                var imageBytes = Convert.FromBase64String(base64Data);
                                column.Item().AlignCenter().Width(150).Height(50)
                                    .Image(imageBytes).FitArea();
                            }
                            catch
                            {
                                // Signature decode failed — leave blank space
                                column.Item().Height(50);
                            }
                        }
                        else
                        {
                            column.Item().Height(50);
                        }

                        column.Item().AlignCenter().PaddingHorizontal(250)
                            .LineHorizontal(1).LineColor(Colors.Grey.Darken2);
                        column.Item().Height(3);
                        column.Item().AlignCenter().Text("Employee Signature")
                            .FontSize(9).FontColor(Colors.Grey.Darken1);

                        column.Item().Height(25);

                        // Footer with certificate number and dates
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"Certificate No: {cert.CertificateNumber}")
                                .FontSize(10).FontColor(Colors.Grey.Darken2);

                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().AlignRight().Text($"Issued: {cert.IssuedAt:MMMM d, yyyy}")
                                    .FontSize(10).FontColor(Colors.Grey.Darken2);

                                if (cert.ExpiresAt.HasValue)
                                {
                                    c.Item().AlignRight().Text($"Valid Until: {cert.ExpiresAt:MMMM d, yyyy}")
                                        .FontSize(10).FontColor(Colors.Grey.Darken2);
                                }
                            });
                        });
                    });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }
}
