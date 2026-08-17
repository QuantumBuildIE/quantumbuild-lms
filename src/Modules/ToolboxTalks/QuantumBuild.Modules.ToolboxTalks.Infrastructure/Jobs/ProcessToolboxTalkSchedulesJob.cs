using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ITenantRepository _tenantRepository;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProcessToolboxTalkSchedulesJob> _logger;

    public ProcessToolboxTalkSchedulesJob(
        IToolboxTalksDbContext dbContext,
        ITenantRepository tenantRepository,
        IServiceScopeFactory scopeFactory,
        ILogger<ProcessToolboxTalkSchedulesJob> logger)
    {
        _dbContext = dbContext;
        _tenantRepository = tenantRepository;
        _scopeFactory = scopeFactory;
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
                        var result = await ProcessScheduleInFreshScopeAsync(tenant.Id, schedule.Id, cancellationToken);

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

    /// <summary>
    /// Dispatches ProcessToolboxTalkScheduleCommand for a single schedule in its own DI scope.
    /// Hangfire has no HttpContext, so ICurrentUserService.TenantId would otherwise resolve to
    /// Guid.Empty inside this scope: the EF Core tenant query filter on ToolboxTalkSchedules
    /// (and everything else ProcessToolboxTalkScheduleCommandHandler touches) would then match
    /// nothing, even for a schedule this job's own selection query just found with an explicit
    /// TenantId. Setting the tenant here, before anything is resolved from this scope, makes the
    /// ambient filter resolve correctly for the unmodified handler, mirroring BulkSopImportJob's
    /// fresh-scope-per-item pattern. A fresh scope also gives each schedule its own DbContext, so
    /// one schedule's failed SaveChangesAsync cannot poison the change tracker for the next.
    /// </summary>
    private async Task<ProcessToolboxTalkScheduleResult> ProcessScheduleInFreshScopeAsync(
        Guid tenantId,
        Guid scheduleId,
        CancellationToken cancellationToken)
    {
        await using var scheduleScope = _scopeFactory.CreateAsyncScope();

        scheduleScope.ServiceProvider.GetRequiredService<IJobTenantContextAccessor>().TenantId = tenantId;

        var mediator = scheduleScope.ServiceProvider.GetRequiredService<IMediator>();

        var command = new ProcessToolboxTalkScheduleCommand
        {
            TenantId = tenantId,
            ScheduleId = scheduleId
        };

        return await mediator.Send(command, cancellationToken);
    }
}
