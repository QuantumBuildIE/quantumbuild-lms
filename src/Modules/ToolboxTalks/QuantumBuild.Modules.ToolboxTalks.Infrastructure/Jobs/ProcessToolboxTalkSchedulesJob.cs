using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.ProcessToolboxTalkSchedule;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Hangfire background job for processing toolbox talk schedules.
/// Runs daily at 6:30 AM to create scheduled talk assignments for due schedules.
/// </summary>
public class ProcessToolboxTalkSchedulesJob
{
    private readonly IMediator _mediator;
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<ProcessToolboxTalkSchedulesJob> _logger;

    public ProcessToolboxTalkSchedulesJob(
        IMediator mediator,
        IToolboxTalksDbContext dbContext,
        ITenantRepository tenantRepository,
        ILogger<ProcessToolboxTalkSchedulesJob> logger)
    {
        _mediator = mediator;
        _dbContext = dbContext;
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    /// <summary>
    /// Executes the schedule processing job.
    /// Finds active schedules that are due and processes them to create assignments.
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting ProcessToolboxTalkSchedulesJob");

        var today = DateTime.UtcNow.Date;
        var processedCount = 0;
        var errorCount = 0;

        // Get all active tenants
        var tenants = await _tenantRepository.GetAllActiveAsync(cancellationToken);

        foreach (var tenant in tenants)
        {
            try
            {
                // NOTE: We use IgnoreQueryFilters() because this runs in a Hangfire background job
                // where ICurrentUserService.TenantId is not set. We explicitly filter by TenantId and IsDeleted.
                var schedulesToProcess = await _dbContext.ToolboxTalkSchedules
                    .IgnoreQueryFilters()
                    .Where(s => s.TenantId == tenant.Id && !s.IsDeleted)
                    .Where(s => s.Status == ToolboxTalkScheduleStatus.Active)
                    .Where(s => s.ScheduledDate.Date <= today ||
                               (s.NextRunDate.HasValue && s.NextRunDate.Value.Date <= today))
                    .ToListAsync(cancellationToken);

                _logger.LogInformation(
                    "Found {Count} schedules to process for tenant {TenantId} ({TenantName})",
                    schedulesToProcess.Count,
                    tenant.Id,
                    tenant.Name);

                foreach (var schedule in schedulesToProcess)
                {
                    try
                    {
                        var command = new ProcessToolboxTalkScheduleCommand
                        {
                            TenantId = tenant.Id,
                            ScheduleId = schedule.Id
                        };

                        var result = await _mediator.Send(command, cancellationToken);

                        _logger.LogInformation(
                            "Processed schedule {ScheduleId}: Created {TalksCreated} talks, Completed: {Completed}, NextRun: {NextRunDate}",
                            schedule.Id,
                            result.TalksCreated,
                            result.ScheduleCompleted,
                            result.NextRunDate);

                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Error processing schedule {ScheduleId} for tenant {TenantId}",
                            schedule.Id,
                            tenant.Id);
                        errorCount++;
                        // Continue processing other schedules
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error processing schedules for tenant {TenantId} ({TenantName})",
                    tenant.Id,
                    tenant.Name);
                // Continue processing other tenants
            }
        }

        _logger.LogInformation(
            "Completed ProcessToolboxTalkSchedulesJob. Processed: {ProcessedCount}, Errors: {ErrorCount}",
            processedCount,
            errorCount);
    }
}
