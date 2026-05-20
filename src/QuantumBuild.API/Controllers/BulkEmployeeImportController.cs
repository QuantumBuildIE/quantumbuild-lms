using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Abstractions;
using QuantumBuild.Core.Application.Features.BulkImport;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Domain.Enums;
using QuantumBuild.Core.Infrastructure.Jobs;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;

namespace QuantumBuild.API.Controllers;

// ── Response DTOs ─────────────────────────────────────────────────────────────

public record BulkImportUploadResponseDto(
    Guid SessionId,
    BulkImportValidationSummaryDto Validation);

public record BulkImportValidationSummaryDto(
    int TotalRows,
    int ValidRows,
    int WarningRows,
    int FailedRows,
    IReadOnlyList<BulkImportRowSummaryDto> RowsWithIssues);

public record BulkImportRowSummaryDto(
    int RowNumber,
    string Status,
    IReadOnlyList<string> Messages);

public record BulkImportSessionStatusDto(
    Guid SessionId,
    string Status,
    DateTimeOffset UploadedAt,
    BulkImportValidationSummaryDto? Validation,
    BulkImportProcessingSummaryDto? Processing);

public record BulkImportProcessingSummaryDto(
    int TotalAttempted,
    int CreatedCount,
    int FailedCount,
    int AlreadyExistedCount,
    int InvitationEmailsSent,
    IReadOnlyList<BulkImportOutcomeDto> Rows);

public record BulkImportOutcomeDto(
    int RowNumber,
    string Status,
    Guid? EmployeeId,
    Guid? LinkedUserId,
    bool InvitationEmailSent,
    string? FailureReason);

public record BulkImportConfirmResponseDto(string JobId);

// ── Controller ────────────────────────────────────────────────────────────────

/// <summary>
/// Bulk employee import via CSV.
/// SuperUsers target a specific tenant via the X-Tenant-Id request header.
/// Non-SuperUsers always operate on their own tenant; ICurrentUserService ignores the header
/// for non-SuperUser callers, so cross-tenant access by non-SuperUsers is impossible.
/// </summary>
[ApiController]
[Route("api/employees/bulk-import")]
[Authorize(Policy = "Core.ManageEmployees")]
public class BulkEmployeeImportController : ControllerBase
{
    private const long MaxCsvSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const int MaxRows = 500;
    private static readonly TimeSpan StuckProcessingThreshold = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // Browsers send various content-type values for CSV files; accept common variants.
    private static readonly string[] AllowedCsvContentTypes =
        ["text/csv", "application/csv", "application/vnd.ms-excel", "text/plain"];

    private static readonly string TemplateCsv =
        "FirstName,LastName,Email,CreateUserAccount,Phone,Mobile,JobTitle,Department,StartDate,EndDate,Notes,PreferredLanguage,UserRole\r\n" +
        "John,Smith,john.smith@example.com,Yes,+353 1 234 5678,+353 87 123 4567,Site Manager,Operations,2025-01-15,,New starter,en,Supervisor\r\n" +
        "Jane,Doe,jane.doe@example.com,Yes,,,Operator,Warehouse,2025-02-01,,,en,Operator\r\n" +
        "Mike,Brown,mike.brown@example.com,No,+353 86 111 2222,,Forklift Driver,Warehouse,,,,en,Operator\r\n";

    private readonly ICoreDbContext _context;
    private readonly IBulkEmployeeImportValidationService _validationService;
    private readonly IR2StorageService _storageService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<BulkEmployeeImportController> _logger;

    public BulkEmployeeImportController(
        ICoreDbContext context,
        IBulkEmployeeImportValidationService validationService,
        IR2StorageService storageService,
        ICurrentUserService currentUser,
        ILogger<BulkEmployeeImportController> logger)
    {
        _context = context;
        _validationService = validationService;
        _storageService = storageService;
        _currentUser = currentUser;
        _logger = logger;
    }

    // ── Template ──────────────────────────────────────────────────────────────

    /// <summary>Download the CSV import template (header row + sample rows).</summary>
    [HttpGet("template")]
    public IActionResult GetTemplate()
        => File(Encoding.UTF8.GetBytes(TemplateCsv), "text/csv", "employee-import-template.csv");

    // ── Upload + Validate ─────────────────────────────────────────────────────

    /// <summary>
    /// Upload a CSV, validate it against the tenant's existing data, and return the validation
    /// summary. The session is created in Validated status; call POST /{id}/confirm to begin
    /// record creation.
    /// SuperUsers must supply the X-Tenant-Id header to target a specific tenant.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(10485760)]                              // 10 MB
    [RequestFormLimits(MultipartBodyLengthLimit = 10485760)] // must match RequestSizeLimit
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty)
            return BadRequest(Result.Fail<BulkImportUploadResponseDto>(
                "SuperUsers must supply the X-Tenant-Id header to target a specific tenant."));

        if (file is null || file.Length == 0)
            return BadRequest(Result.Fail<BulkImportUploadResponseDto>("No file provided."));

        if (!AllowedCsvContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            return BadRequest(Result.Fail<BulkImportUploadResponseDto>(
                $"Invalid file type '{file.ContentType}'. Please upload a .csv file."));

        if (file.Length > MaxCsvSizeBytes)
            return BadRequest(Result.Fail<BulkImportUploadResponseDto>(
                $"File size ({file.Length / 1024 / 1024} MB) exceeds the 10 MB maximum."));

        // Pre-generate the session ID so the R2 key is deterministic before the DB insert.
        var sessionId = Guid.NewGuid();

        // Validate — reads the stream; must be reopened for the upload below.
        BulkImportValidationResult validationResult;
        await using (var csvStream = file.OpenReadStream())
        {
            validationResult = await _validationService.ValidateAsync(csvStream, ct);
        }

        // TotalRows == 0 indicates a structural error (missing required columns, parse failure).
        if (validationResult.TotalRows == 0)
            return BadRequest(Result.Fail<BulkImportUploadResponseDto>(
                validationResult.Rows.FirstOrDefault()?.Messages.FirstOrDefault()
                ?? "File could not be parsed. Ensure column headers match the template."));

        if (validationResult.TotalRows > MaxRows)
            return BadRequest(Result.Fail<BulkImportUploadResponseDto>(
                $"File contains {validationResult.TotalRows} rows; maximum per import is {MaxRows}."));

        // Upload CSV to R2 — fresh stream after validation consumed the previous one.
        string csvKey;
        await using (var uploadStream = file.OpenReadStream())
        {
            csvKey = await _storageService.UploadBulkImportCsvAsync(tenantId, sessionId, uploadStream, ct);
        }

        var session = new BulkImportSession
        {
            Id = sessionId,
            TenantId = tenantId,
            CsvR2Key = csvKey,
            Status = BulkImportStatus.Validated,
            UploadedAt = DateTimeOffset.UtcNow,
            ValidationResultJson = JsonSerializer.Serialize(validationResult, JsonOpts),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        _context.BulkImportSessions.Add(session);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "BulkImport upload: session {SessionId} for tenant {TenantId} — " +
            "{Total} rows ({Valid} valid, {Warnings} warning, {Failed} failed)",
            sessionId, tenantId,
            validationResult.TotalRows, validationResult.ValidRows,
            validationResult.WarningRows, validationResult.FailedRows);

        return Ok(Result.Ok(new BulkImportUploadResponseDto(sessionId, ToValidationSummary(validationResult))));
    }

    // ── Confirm / Re-trigger ──────────────────────────────────────────────────

    /// <summary>
    /// Enqueue the BulkEmployeeImportJob for a Validated session.
    /// Recovery: a session stuck in Processing for more than 30 minutes (indicative of a
    /// process crash) is reset to Validated and re-enqueued. A session that is genuinely
    /// in-flight (within the 30-minute window) returns 409 Conflict.
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
    {
        var session = await GetOwnedSessionAsync(id, ct);
        if (session is null)
            return NotFound(Result.Fail<BulkImportConfirmResponseDto>("Import session not found."));

        if (session.Status == BulkImportStatus.Processing)
        {
            var elapsed = DateTimeOffset.UtcNow - session.ProcessingStartedAt!.Value;

            if (elapsed < StuckProcessingThreshold)
                return Conflict(Result.Fail<BulkImportConfirmResponseDto>(
                    "The import job is currently in progress. Check back in a few minutes."));

            // Session is stuck after a process crash — reset so the job can run again.
            // Mark IsRerun = true so the job classifies duplicate-email failures as
            // AlreadyExisted rather than Failed (those rows succeeded in the first run).
            _logger.LogWarning(
                "BulkImport confirm: session {SessionId} stuck in Processing for {Minutes:F0} min — re-triggering as re-run",
                id, elapsed.TotalMinutes);

            session.Status = BulkImportStatus.Validated;
            session.IsRerun = true;
            session.ProcessingStartedAt = null;
            session.UpdatedAt = DateTime.UtcNow;
            session.UpdatedBy = _currentUser.UserId;
            await _context.SaveChangesAsync(ct);
        }
        else if (session.Status != BulkImportStatus.Validated)
        {
            return BadRequest(Result.Fail<BulkImportConfirmResponseDto>(
                $"Session is '{session.Status}' and cannot be confirmed. Only Validated sessions can be started."));
        }

        // Enqueue via the concrete class so Hangfire reads [AutomaticRetry(Attempts=0)]
        // from the class-level attribute. Enqueueing via the interface loses class attributes.
        var jobId = BackgroundJob.Enqueue<BulkEmployeeImportJob>(
            j => j.ExecuteAsync(id, CancellationToken.None));

        _logger.LogInformation(
            "BulkImport confirm: session {SessionId} enqueued as Hangfire job {JobId}", id, jobId);

        return Ok(Result.Ok(new BulkImportConfirmResponseDto(jobId)));
    }

    // ── Status ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Get the current status, validation summary, and processing results for a session.
    /// The Stage 3 page polls this endpoint while the job is in Processing status.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken ct)
    {
        var session = await GetOwnedSessionAsync(id, ct);
        if (session is null)
            return NotFound(Result.Fail<BulkImportSessionStatusDto>("Import session not found."));

        var validationSummary = session.ValidationResultJson is null
            ? null
            : ToValidationSummary(
                JsonSerializer.Deserialize<BulkImportValidationResult>(session.ValidationResultJson, JsonOpts)!);

        var processingSummary = session.ProcessingResultJson is null
            ? null
            : ToProcessingSummary(
                JsonSerializer.Deserialize<BulkImportProcessingResult>(session.ProcessingResultJson, JsonOpts)!);

        return Ok(Result.Ok(new BulkImportSessionStatusDto(
            session.Id,
            session.Status.ToString(),
            session.UploadedAt,
            validationSummary,
            processingSummary)));
    }

    // ── Failed Rows Download ──────────────────────────────────────────────────

    /// <summary>
    /// Download a CSV of failed rows only — same column layout as the import template, with
    /// an appended Error column. The user can correct these rows and re-upload.
    /// Source: if processing results exist, returns rows that failed during record creation;
    /// otherwise returns rows that failed validation.
    /// </summary>
    [HttpGet("{id:guid}/failed-rows")]
    public async Task<IActionResult> DownloadFailedRows(Guid id, CancellationToken ct)
    {
        var session = await GetOwnedSessionAsync(id, ct);
        if (session is null)
            return NotFound(Result.Fail("Import session not found."));

        if (session.ValidationResultJson is null)
            return BadRequest(Result.Fail("Session has no validation results yet."));

        var validationResult = JsonSerializer.Deserialize<BulkImportValidationResult>(
            session.ValidationResultJson, JsonOpts)!;

        BulkImportProcessingResult? processingResult = null;
        if (session.ProcessingResultJson is not null)
            processingResult = JsonSerializer.Deserialize<BulkImportProcessingResult>(
                session.ProcessingResultJson, JsonOpts)!;

        var csvBytes = BuildFailedRowsCsv(validationResult, processingResult);
        if (csvBytes is null)
            return BadRequest(Result.Fail("No failed rows to download."));

        return File(csvBytes, "text/csv", $"import-failed-rows-{id:N}.csv");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a session, relying on the EF Core query filter for tenant isolation.
    /// For SuperUsers with X-Tenant-Id header, the filter scopes to that tenant.
    /// For SuperUsers without the header, BypassTenantFilter is true (all tenants visible).
    /// </summary>
    private Task<BulkImportSession?> GetOwnedSessionAsync(Guid id, CancellationToken ct)
        => _context.BulkImportSessions
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);

    private static BulkImportValidationSummaryDto ToValidationSummary(BulkImportValidationResult r) =>
        new(
            r.TotalRows,
            r.ValidRows,
            r.WarningRows,
            r.FailedRows,
            r.Rows
                .Where(row => row.Messages.Count > 0)
                .Select(row => new BulkImportRowSummaryDto(row.RowNumber, row.Status.ToString(), row.Messages))
                .ToList());

    private static BulkImportProcessingSummaryDto ToProcessingSummary(BulkImportProcessingResult r) =>
        new(
            r.TotalAttempted,
            r.CreatedCount,
            r.FailedCount,
            r.AlreadyExistedCount,
            r.InvitationEmailsSent,
            r.Rows.Select(o => new BulkImportOutcomeDto(
                o.RowNumber,
                o.Status.ToString(),
                o.EmployeeId,
                o.LinkedUserId,
                o.InvitationEmailSent,
                o.FailureReason)).ToList());

    /// <summary>
    /// Builds a correctable CSV of failed rows. Returns null if there are no failures.
    /// Prefers processing failures (post-creation errors) over validation failures when both exist.
    /// </summary>
    private static byte[]? BuildFailedRowsCsv(
        BulkImportValidationResult validationResult,
        BulkImportProcessingResult? processingResult)
    {
        const string Header =
            "FirstName,LastName,Email,CreateUserAccount,Phone,Mobile,JobTitle,Department," +
            "StartDate,EndDate,Notes,PreferredLanguage,UserRole,Error";

        List<(BulkImportRowResult Row, string Error)> failedRows;

        if (processingResult is not null)
        {
            // AlreadyExisted rows are excluded: those emails were created successfully in
            // an interrupted first run and do not need correcting or re-uploading.
            var failedByRowNumber = processingResult.Rows
                .Where(o => o.Status == BulkImportRowOutcomeStatus.Failed)
                .ToDictionary(o => o.RowNumber, o => o.FailureReason ?? "Unknown error");

            failedRows = validationResult.Rows
                .Where(r => failedByRowNumber.ContainsKey(r.RowNumber))
                .Select(r => (r, failedByRowNumber[r.RowNumber]))
                .ToList();
        }
        else
        {
            failedRows = validationResult.Rows
                .Where(r => r.Status == BulkImportRowStatus.Failed)
                .Select(r => (r, string.Join("; ", r.Messages)))
                .ToList();
        }

        if (failedRows.Count == 0)
            return null;

        var sb = new StringBuilder();
        sb.AppendLine(Header);

        foreach (var (row, error) in failedRows)
        {
            sb.AppendLine(string.Join(",",
                CsvEscape(row.FirstName),
                CsvEscape(row.LastName),
                CsvEscape(row.Email),
                row.CreateUserAccount ? "Yes" : "No",
                CsvEscape(row.Phone),
                CsvEscape(row.Mobile),
                CsvEscape(row.JobTitle),
                CsvEscape(row.Department),
                row.StartDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                row.EndDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                CsvEscape(row.Notes),
                CsvEscape(row.PreferredLanguage),
                CsvEscape(row.UserRole),
                CsvEscape(error)));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
