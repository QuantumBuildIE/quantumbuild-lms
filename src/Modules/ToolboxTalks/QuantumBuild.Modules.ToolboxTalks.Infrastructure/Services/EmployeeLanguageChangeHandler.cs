using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;

/// <summary>
/// Handles employee language changes by checking if the language is new to the
/// tenant and, if so, enqueuing translation jobs for the employee's assigned talks.
/// </summary>
public class EmployeeLanguageChangeHandler : IEmployeeLanguageChangeHandler
{
    private readonly ICoreDbContext _coreDbContext;
    private readonly IToolboxTalksDbContext _toolboxTalksDbContext;
    private readonly ILogger<EmployeeLanguageChangeHandler> _logger;

    public EmployeeLanguageChangeHandler(
        ICoreDbContext coreDbContext,
        IToolboxTalksDbContext toolboxTalksDbContext,
        ILogger<EmployeeLanguageChangeHandler> logger)
    {
        _coreDbContext = coreDbContext;
        _toolboxTalksDbContext = toolboxTalksDbContext;
        _logger = logger;
    }

    public async Task HandleLanguageChangeAsync(
        Guid tenantId,
        Guid employeeId,
        string preferredLanguage,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(preferredLanguage)
            || string.Equals(preferredLanguage, "en", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Check if this language is already spoken by another active employee in the tenant
        var isNewLanguageForTenant = !await _coreDbContext.Employees
            .AnyAsync(e => e.TenantId == tenantId
                && !e.IsDeleted
                && e.Id != employeeId
                && e.PreferredLanguage == preferredLanguage, ct);

        if (!isNewLanguageForTenant)
            return;

        // Get all unique ToolboxTalkIds assigned to this employee (non-cancelled)
        var assignedTalkIds = await _toolboxTalksDbContext.ScheduledTalks
            .IgnoreQueryFilters()
            .Where(st => st.TenantId == tenantId
                && !st.IsDeleted
                && st.EmployeeId == employeeId
                && st.Status != ScheduledTalkStatus.Cancelled)
            .Select(st => st.ToolboxTalkId)
            .Distinct()
            .ToListAsync(ct);

        if (assignedTalkIds.Count == 0)
        {
            _logger.LogInformation(
                "New language {Language} detected for tenant {TenantId} via employee {EmployeeId}, but no assigned talks to translate",
                preferredLanguage, tenantId, employeeId);
            return;
        }

        foreach (var talkId in assignedTalkIds)
        {
            BackgroundJob.Enqueue<MissingTranslationsJob>(
                job => job.ExecuteAsync(talkId, tenantId, null, CancellationToken.None));
        }

        _logger.LogInformation(
            "New language {Language} detected for tenant {TenantId}. Queuing translations for {Count} assigned talks for employee {EmployeeId}",
            preferredLanguage, tenantId, assignedTalkIds.Count, employeeId);
    }
}
