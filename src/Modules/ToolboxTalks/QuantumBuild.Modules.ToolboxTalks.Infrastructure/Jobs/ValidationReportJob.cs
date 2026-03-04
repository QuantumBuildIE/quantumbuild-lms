using Hangfire;
using Microsoft.Extensions.Logging;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Hangfire background job that generates the audit report PDF for a translation validation run.
/// Stub — full implementation will be added in a future phase.
/// </summary>
public class ValidationReportJob
{
    private readonly ILogger<ValidationReportJob> _logger;

    public ValidationReportJob(ILogger<ValidationReportJob> logger)
    {
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 1)]
    [Queue("content-generation")]
    public async Task ExecuteAsync(
        Guid validationRunId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "ValidationReportJob started for run {RunId}, tenant {TenantId}. " +
            "Full implementation pending.",
            validationRunId, tenantId);

        await Task.CompletedTask;
    }
}
