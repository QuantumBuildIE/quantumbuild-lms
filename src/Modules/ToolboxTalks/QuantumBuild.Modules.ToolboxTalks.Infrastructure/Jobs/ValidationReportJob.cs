using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Hangfire background job that generates the audit report PDF for a translation validation run,
/// uploads it to R2, and stores the public URL on the run entity.
/// </summary>
public class ValidationReportJob(
    IToolboxTalksDbContext dbContext,
    IValidationReportService reportService,
    IR2StorageService storageService,
    ILogger<ValidationReportJob> logger)
{
    [AutomaticRetry(Attempts = 1)]
    [Queue("content-generation")]
    public async Task ExecuteAsync(
        Guid validationRunId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "ValidationReportJob started for run {RunId}, tenant {TenantId}",
            validationRunId, tenantId);

        // Load the run with all results and the associated talk
        var run = await dbContext.TranslationValidationRuns
            .IgnoreQueryFilters()
            .Include(r => r.Results.OrderBy(res => res.SectionIndex))
            .Include(r => r.ToolboxTalk)
            .FirstOrDefaultAsync(r => r.Id == validationRunId && r.TenantId == tenantId, cancellationToken);

        if (run == null)
        {
            logger.LogWarning("Validation run {RunId} not found for tenant {TenantId}", validationRunId, tenantId);
            return;
        }

        try
        {
            // Generate the PDF
            var pdfBytes = await reportService.GenerateAsync(run, cancellationToken);

            logger.LogInformation(
                "Generated report PDF for run {RunId} ({Size} bytes). Uploading to R2...",
                validationRunId, pdfBytes.Length);

            // Upload to R2: {tenantId}/validation-reports/{runId}.pdf
            var fileName = $"{validationRunId}.pdf";
            using var stream = new MemoryStream(pdfBytes);

            var result = await storageService.UploadValidationReportAsync(
                tenantId, validationRunId, stream, cancellationToken);

            if (!result.Success)
            {
                logger.LogError(
                    "Failed to upload validation report PDF for run {RunId}: {Error}",
                    validationRunId, result.ErrorMessage);
                throw new InvalidOperationException(
                    $"R2 upload failed for validation report {validationRunId}: {result.ErrorMessage}");
            }

            // Update the run with the public URL
            run.AuditReportUrl = result.PublicUrl;
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Validation report uploaded for run {RunId}: {Url}",
                validationRunId, result.PublicUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "ValidationReportJob failed for run {RunId}, tenant {TenantId}",
                validationRunId, tenantId);
            throw;
        }
    }
}
