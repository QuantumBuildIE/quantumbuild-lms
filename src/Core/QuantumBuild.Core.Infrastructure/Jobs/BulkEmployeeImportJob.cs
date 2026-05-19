using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Core.Application.Abstractions;
using QuantumBuild.Core.Application.Features.BulkImport;
using QuantumBuild.Core.Application.Features.Employees;
using QuantumBuild.Core.Application.Features.Employees.DTOs;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;

namespace QuantumBuild.Core.Infrastructure.Jobs;

/// <summary>
/// Reads the validated rows from a BulkImportSession and creates Employee (and optionally
/// User) records. Invitation emails are sent after all records are created, throttled to
/// stay within the MailerSend provider rate limit (configurable via BulkImport:InvitationEmailDelayMs).
///
/// Partial-failure model: each row is independent. A row-level failure is recorded and
/// the job continues to the next row. The session transitions to Completed once the job
/// finishes (even with some failed rows). Only a job-level exception causes Failed status.
///
/// AutomaticRetry is disabled: the session is already in Processing when the job runs,
/// and a retry would attempt to re-create rows that may have already succeeded.
/// Stage 4 will expose an endpoint to trigger a re-run on a Failed session.
/// </summary>
[AutomaticRetry(Attempts = 0)]
public class BulkEmployeeImportJob : IBulkEmployeeImportJob
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly ICoreDbContext _context;
    private readonly IEmployeeService _employeeService;
    private readonly IEmailService _emailService;
    private readonly UserManager<User> _userManager;
    private readonly IR2StorageService _storageService;
    private readonly BulkImportSettings _settings;
    private readonly ILogger<BulkEmployeeImportJob> _logger;

    public BulkEmployeeImportJob(
        ICoreDbContext context,
        IEmployeeService employeeService,
        IEmailService emailService,
        UserManager<User> userManager,
        IR2StorageService storageService,
        IOptions<BulkImportSettings> settings,
        ILogger<BulkEmployeeImportJob> logger)
    {
        _context = context;
        _employeeService = employeeService;
        _emailService = emailService;
        _userManager = userManager;
        _storageService = storageService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid sessionId, CancellationToken ct)
    {
        _logger.LogInformation("BulkEmployeeImportJob starting for session {SessionId}", sessionId);

        // Bypass tenant query filter — Hangfire has no HTTP context so ICurrentUserService
        // returns null tenant. Filter explicitly by ID instead.
        var session = await _context.BulkImportSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == sessionId && !s.IsDeleted, ct);

        if (session is null)
        {
            _logger.LogError("BulkEmployeeImportJob: session {SessionId} not found", sessionId);
            return;
        }

        if (session.Status != BulkImportStatus.Validated)
        {
            _logger.LogWarning(
                "BulkEmployeeImportJob: session {SessionId} is in status {Status}, expected Validated — aborting",
                sessionId, session.Status);
            return;
        }

        var tenantId = session.TenantId;

        // Mark Processing immediately so the admin can see it and to block double-runs.
        // ProcessingStartedAt is persisted here so Stage 4 can detect sessions stuck in
        // Processing after a mid-job process restart (no automatic recovery in Stage 2).
        session.Status = BulkImportStatus.Processing;
        session.ProcessingStartedAt = DateTimeOffset.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        var outcomes = new List<BulkImportRowOutcome>();
        var pendingInvitations = new List<PendingInvitation>();

        try
        {
            if (string.IsNullOrWhiteSpace(session.ValidationResultJson))
                throw new InvalidOperationException("Session has no ValidationResultJson — cannot process.");

            var validationResult = JsonSerializer.Deserialize<BulkImportValidationResult>(
                session.ValidationResultJson, JsonOpts)
                ?? throw new InvalidOperationException("ValidationResultJson deserialized to null.");

            var rowsToProcess = validationResult.Rows
                .Where(r => r.Status != BulkImportRowStatus.Failed)
                .ToList();

            _logger.LogInformation(
                "BulkEmployeeImportJob: session {SessionId} — {Count} rows to process for tenant {TenantId}",
                sessionId, rowsToProcess.Count, tenantId);

            // --- Creation phase ---
            // Each row is independent: failures are recorded and the loop continues.
            foreach (var row in rowsToProcess)
            {
                ct.ThrowIfCancellationRequested();
                await ProcessRowAsync(row, tenantId, outcomes, pendingInvitations, sessionId);
            }

            // --- Rate-limited invitation email phase ---
            // All records are committed before any email is sent. This decouples creation
            // from delivery and keeps the provider well under its rate limit.
            int emailsSent = 0;
            var sentRowNumbers = new HashSet<int>();

            for (int i = 0; i < pendingInvitations.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var invitation = pendingInvitations[i];
                var sent = await TrySendInvitationEmailAsync(invitation, ct);
                if (sent)
                {
                    emailsSent++;
                    sentRowNumbers.Add(invitation.RowNumber);
                }

                // Delay between sends except after the last one.
                // Duration comes from BulkImport:InvitationEmailDelayMs in appsettings.
                if (i < pendingInvitations.Count - 1)
                    await Task.Delay(_settings.InvitationEmailDelayMs, ct);
            }

            // Stamp outcomes where the invitation email was actually sent
            for (int i = 0; i < outcomes.Count; i++)
            {
                var o = outcomes[i];
                if (o.Status == BulkImportRowOutcomeStatus.Created && sentRowNumbers.Contains(o.RowNumber))
                    outcomes[i] = o with { InvitationEmailSent = true };
            }

            var processingResult = BuildResult(outcomes, emailsSent);
            session.ProcessingResultJson = JsonSerializer.Serialize(processingResult, JsonOpts);
            session.Status = BulkImportStatus.Completed;
            session.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "BulkEmployeeImportJob: session {SessionId} completed — " +
                "{Created} created, {Failed} failed, {Emails} invitation emails sent",
                sessionId, processingResult.CreatedCount, processingResult.FailedCount, emailsSent);

            // R2 cleanup — delete the CSV now the session is permanently Completed.
            // On failure, leave the file for investigation (see catch block below).
            try
            {
                await _storageService.DeleteFileAsync(session.CsvR2Key, ct);
                _logger.LogInformation(
                    "BulkEmployeeImportJob: deleted CSV {Key} for session {SessionId}",
                    session.CsvR2Key, sessionId);
            }
            catch (Exception cleanupEx)
            {
                // Non-fatal — session is already Completed; CSV can be cleaned up manually.
                _logger.LogWarning(cleanupEx,
                    "BulkEmployeeImportJob: failed to delete CSV {Key} for session {SessionId} — leaving for manual cleanup",
                    session.CsvR2Key, sessionId);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "BulkEmployeeImportJob: job-level failure for session {SessionId}", sessionId);

            // Preserve whatever partial outcomes exist so the admin can see what ran
            if (outcomes.Count > 0)
            {
                var partialResult = BuildResult(outcomes, emailsSent: 0);
                session.ProcessingResultJson = JsonSerializer.Serialize(partialResult, JsonOpts);
            }

            // CSV is intentionally left in R2 on job-level failure for investigation.
            session.Status = BulkImportStatus.Failed;
            session.UpdatedAt = DateTime.UtcNow;

            try { await _context.SaveChangesAsync(ct); }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx,
                    "BulkEmployeeImportJob: could not persist Failed status for session {SessionId}", sessionId);
            }

            throw;
        }
    }

    private async Task ProcessRowAsync(
        BulkImportRowResult row,
        Guid tenantId,
        List<BulkImportRowOutcome> outcomes,
        List<PendingInvitation> pendingInvitations,
        Guid sessionId)
    {
        try
        {
            var dto = new CreateEmployeeDto(
                EmployeeCode: null,
                FirstName: row.FirstName!,
                LastName: row.LastName!,
                Email: row.Email,
                Phone: row.Phone,
                Mobile: row.Mobile,
                JobTitle: row.JobTitle,
                Department: row.Department,
                PrimarySiteId: null,
                StartDate: row.StartDate.HasValue
                    ? DateTime.SpecifyKind(row.StartDate.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
                    : null,
                EndDate: row.EndDate.HasValue
                    ? DateTime.SpecifyKind(row.EndDate.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
                    : null,
                IsActive: true,
                Notes: row.Notes,
                GeoTrackerID: null,
                CreateUserAccount: row.CreateUserAccount,
                UserRole: row.UserRole,
                PreferredLanguage: row.PreferredLanguage
            );

            var result = await _employeeService.CreateAsync(
                dto,
                sendInvitationEmail: false,
                tenantIdOverride: tenantId);

            if (result.Success)
            {
                outcomes.Add(new BulkImportRowOutcome
                {
                    RowNumber = row.RowNumber,
                    Status = BulkImportRowOutcomeStatus.Created,
                    EmployeeId = result.Data!.Id,
                    LinkedUserId = result.Data.LinkedUserId
                });

                if (row.CreateUserAccount &&
                    result.Data.LinkedUserId.HasValue &&
                    !string.IsNullOrWhiteSpace(row.Email))
                {
                    pendingInvitations.Add(
                        new PendingInvitation(row.Email, row.FirstName!, result.Data.LinkedUserId.Value, row.RowNumber));
                }
            }
            else
            {
                var reason = string.Join("; ", result.Errors);
                _logger.LogWarning(
                    "BulkEmployeeImportJob: row {RowNumber} failed for session {SessionId}: {Reason}",
                    row.RowNumber, sessionId, reason);

                outcomes.Add(new BulkImportRowOutcome
                {
                    RowNumber = row.RowNumber,
                    Status = BulkImportRowOutcomeStatus.Failed,
                    FailureReason = reason
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "BulkEmployeeImportJob: unexpected error on row {RowNumber} for session {SessionId}",
                row.RowNumber, sessionId);

            outcomes.Add(new BulkImportRowOutcome
            {
                RowNumber = row.RowNumber,
                Status = BulkImportRowOutcomeStatus.Failed,
                FailureReason = ex.Message
            });
        }
    }

    private async Task<bool> TrySendInvitationEmailAsync(PendingInvitation invitation, CancellationToken ct)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(invitation.UserId.ToString());
            if (user is null)
            {
                _logger.LogWarning(
                    "BulkEmployeeImportJob: user {UserId} not found when sending invitation to row {Row} ({Email})",
                    invitation.UserId, invitation.RowNumber, invitation.Email);
                return false;
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _emailService.SendPasswordSetupEmailAsync(
                invitation.Email,
                invitation.FirstName,
                token,
                cancellationToken: ct);

            _logger.LogInformation(
                "BulkEmployeeImportJob: sent invitation email to {Email} (row {Row})",
                invitation.Email, invitation.RowNumber);
            return true;
        }
        catch (Exception ex)
        {
            // Non-fatal: log and continue to the next invitation
            _logger.LogWarning(ex,
                "BulkEmployeeImportJob: failed to send invitation to {Email} (row {Row}) — continuing",
                invitation.Email, invitation.RowNumber);
            return false;
        }
    }

    private static BulkImportProcessingResult BuildResult(List<BulkImportRowOutcome> outcomes, int emailsSent) =>
        new()
        {
            TotalAttempted = outcomes.Count,
            CreatedCount = outcomes.Count(o => o.Status == BulkImportRowOutcomeStatus.Created),
            FailedCount = outcomes.Count(o => o.Status == BulkImportRowOutcomeStatus.Failed),
            InvitationEmailsSent = emailsSent,
            Rows = outcomes
        };

    private sealed record PendingInvitation(string Email, string FirstName, Guid UserId, int RowNumber);
}
