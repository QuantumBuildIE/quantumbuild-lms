using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Abstractions;
using QuantumBuild.Core.Application.Interfaces;

namespace QuantumBuild.Core.Infrastructure.Jobs;

/// <summary>
/// One-time job enqueued when QrLocationTrainingEnabled is first set to true for a tenant.
/// Generates PINs for all active employees without a PIN and emails each one.
/// </summary>
[AutomaticRetry(Attempts = 1)]
public class GenerateEmployeePinsJob : IGenerateEmployeePinsJob
{
    private readonly ICoreDbContext _context;
    private readonly IEmployeePinService _pinService;
    private readonly IEmailService _emailService;
    private readonly ILogger<GenerateEmployeePinsJob> _logger;

    public GenerateEmployeePinsJob(
        ICoreDbContext context,
        IEmployeePinService pinService,
        IEmailService emailService,
        ILogger<GenerateEmployeePinsJob> logger)
    {
        _context = context;
        _pinService = pinService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid tenantId, CancellationToken ct)
    {
        _logger.LogInformation(
            "GenerateEmployeePinsJob starting for Tenant {TenantId}", tenantId);

        // Load active employees without a PIN — ignore soft-delete & tenant query filters
        // since Hangfire has no HTTP context, then filter manually.
        var employees = await _context.Employees
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.IsActive && !e.QrPinIsSet)
            .ToListAsync(ct);

        _logger.LogInformation(
            "GenerateEmployeePinsJob found {Count} employees needing a PIN for Tenant {TenantId}",
            employees.Count, tenantId);

        // Resolve tenant name once for the email subject
        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct);
        var tenantName = tenant ?? "your organisation";

        int processed = 0;

        foreach (var employee in employees)
        {
            try
            {
                var rawPin = await _pinService.ResetPinAsync(employee, ct);

                if (!string.IsNullOrWhiteSpace(employee.Email))
                {
                    await _emailService.SendPinEmailAsync(
                        email: employee.Email,
                        firstName: employee.FirstName,
                        qrPin: rawPin,
                        subject: $"Your workstation access PIN — {tenantName}",
                        introText: $"QR Location Training has been enabled at {tenantName}. " +
                                   "You have been assigned the workstation access PIN below. " +
                                   "Use it to identify yourself when you scan a QR code at a training station or site entrance. " +
                                   "If you ever need a new PIN, contact your administrator.",
                        cancellationToken: ct);

                    _logger.LogInformation(
                        "Sent QR PIN introduction email to Employee {EmployeeId} ({Email})",
                        employee.Id, employee.Email);
                }
                else
                {
                    _logger.LogInformation(
                        "Skipped email for Employee {EmployeeId} — no email address",
                        employee.Id);
                }

                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to generate/send PIN for Employee {EmployeeId} in Tenant {TenantId}",
                    employee.Id, tenantId);
                // Continue with remaining employees
            }
        }

        _logger.LogInformation(
            "GenerateEmployeePinsJob completed for Tenant {TenantId}: {Processed}/{Total} processed",
            tenantId, processed, employees.Count);
    }
}
