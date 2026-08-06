using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Abstractions;
using QuantumBuild.Core.Application.Features.BulkSopImport;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Domain.Enums;
using QuantumBuild.Core.Infrastructure.Jobs;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;

namespace QuantumBuild.API.Controllers;

// ── Response DTOs ─────────────────────────────────────────────────────────────

public record BulkSopImportUploadResponseDto(
    Guid SessionId,
    BulkSopImportValidationSummaryDto Validation);

public record BulkSopImportValidationSummaryDto(
    int TotalPdfCount,
    long TotalUncompressedBytes,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> IgnoredEntryNames);

public record BulkSopImportSessionStatusDto(
    Guid SessionId,
    string Status,
    DateTimeOffset UploadedAt,
    BulkSopImportValidationSummaryDto? Validation,
    BulkSopImportProcessingSummaryDto? Processing);

public record BulkSopImportProcessingSummaryDto(
    int TotalAttempted,
    int SucceededCount,
    int FailedCount,
    int AlreadyExistedCount,
    IReadOnlyList<BulkSopImportOutcomeDto> Items);

public record BulkSopImportOutcomeDto(
    int ItemIndex,
    string FileName,
    string Status,
    Guid? ToolboxTalkId,
    string? ToolboxTalkTitle,
    string? FailureReason,
    string? Warning);

public record BulkSopImportConfirmResponseDto(string JobId);

// ── Controller ────────────────────────────────────────────────────────────────

/// <summary>
/// Bulk creation of Draft learnings from a ZIP of SOP PDFs.
/// SuperUsers target a specific tenant via the X-Tenant-Id request header.
/// Non-SuperUsers always operate on their own tenant.
/// Each PDF becomes a learning via the same initialise -> parse -> quiz-generate chain the
/// new learning-wizard uses. Translation is out of scope — learnings land as Draft with no
/// target languages, exactly as they would if a wizard user stopped before the Translate step.
/// </summary>
[ApiController]
[Route("api/toolbox-talks/bulk-sop-import")]
[Authorize(Policy = "Learnings.Manage")]
public class BulkSopImportController : ControllerBase
{
    private const long MaxZipSizeBytes = 200L * 1024 * 1024; // 200 MB — compressed upload size
    private static readonly TimeSpan StuckProcessingThreshold = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static readonly string[] AllowedZipContentTypes =
        ["application/zip", "application/x-zip-compressed", "application/octet-stream", "multipart/x-zip"];

    private readonly ICoreDbContext _context;
    private readonly IBulkSopImportValidationService _validationService;
    private readonly IR2StorageService _storageService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<BulkSopImportController> _logger;

    public BulkSopImportController(
        ICoreDbContext context,
        IBulkSopImportValidationService validationService,
        IR2StorageService storageService,
        ICurrentUserService currentUser,
        ILogger<BulkSopImportController> logger)
    {
        _context = context;
        _validationService = validationService;
        _storageService = storageService;
        _currentUser = currentUser;
        _logger = logger;
    }

    // ── Upload + Validate ─────────────────────────────────────────────────────

    /// <summary>
    /// Upload a ZIP of SOP PDFs, validate it, and return a summary. The session is created in
    /// Validated status; call POST /{id}/confirm to begin learning creation.
    /// SuperUsers must supply the X-Tenant-Id header to target a specific tenant.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(209715200)]                              // 200 MB
    [RequestFormLimits(MultipartBodyLengthLimit = 209715200)] // must match RequestSizeLimit
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty)
            return BadRequest(Result.Fail<BulkSopImportUploadResponseDto>(
                "SuperUsers must supply the X-Tenant-Id header to target a specific tenant."));

        if (file is null || file.Length == 0)
            return BadRequest(Result.Fail<BulkSopImportUploadResponseDto>("No file provided."));

        if (!AllowedZipContentTypes.Contains(file.ContentType.ToLowerInvariant())
            && !file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return BadRequest(Result.Fail<BulkSopImportUploadResponseDto>(
                $"Invalid file type '{file.ContentType}'. Please upload a .zip file."));

        if (file.Length > MaxZipSizeBytes)
            return BadRequest(Result.Fail<BulkSopImportUploadResponseDto>(
                $"File size ({file.Length / 1024 / 1024} MB) exceeds the {MaxZipSizeBytes / 1024 / 1024} MB maximum."));

        var sessionId = Guid.NewGuid();

        BulkSopImportValidationResult validationResult;
        await using (var zipStream = file.OpenReadStream())
        {
            validationResult = await _validationService.ValidateAsync(zipStream, ct);
        }

        if (!validationResult.ArchiveReadable)
            return BadRequest(Result.Fail<BulkSopImportUploadResponseDto>(
                validationResult.ErrorMessage ?? "The archive could not be validated."));

        await using var uploadStream = file.OpenReadStream();
        var uploadResult = await _storageService.UploadSessionFileAsync(
            tenantId, sessionId, uploadStream, "upload.zip", "application/zip", ct);

        if (!uploadResult.Success || string.IsNullOrEmpty(uploadResult.Key))
            return BadRequest(Result.Fail<BulkSopImportUploadResponseDto>(
                $"Failed to store the archive: {uploadResult.ErrorMessage ?? "unknown storage error"}"));

        var session = new BulkSopImportSession
        {
            Id = sessionId,
            TenantId = tenantId,
            ZipR2Key = uploadResult.Key,
            Status = BulkSopImportStatus.Validated,
            UploadedAt = DateTimeOffset.UtcNow,
            ValidationResultJson = JsonSerializer.Serialize(validationResult, JsonOpts),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        _context.BulkSopImportSessions.Add(session);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "BulkSopImport upload: session {SessionId} for tenant {TenantId} — " +
            "{Total} PDFs queued, {Ignored} entries ignored",
            sessionId, tenantId, validationResult.TotalPdfCount, validationResult.IgnoredEntryNames.Count);

        return Ok(Result.Ok(new BulkSopImportUploadResponseDto(sessionId, ToValidationSummary(validationResult))));
    }

    // ── Confirm / Re-trigger ──────────────────────────────────────────────────

    /// <summary>
    /// Enqueue the BulkSopImportJob for a Validated session.
    /// Recovery: a session stuck in Processing for more than 30 minutes (indicative of a
    /// process crash) is reset to Validated and re-enqueued. A session that is genuinely
    /// in-flight (within the 30-minute window) returns 409 Conflict.
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
    {
        var session = await GetOwnedSessionAsync(id, ct);
        if (session is null)
            return NotFound(Result.Fail<BulkSopImportConfirmResponseDto>("Import session not found."));

        if (session.Status == BulkSopImportStatus.Processing)
        {
            var elapsed = DateTimeOffset.UtcNow - session.ProcessingStartedAt!.Value;

            if (elapsed < StuckProcessingThreshold)
                return Conflict(Result.Fail<BulkSopImportConfirmResponseDto>(
                    "The import job is currently in progress. Check back in a few minutes."));

            _logger.LogWarning(
                "BulkSopImport confirm: session {SessionId} stuck in Processing for {Minutes:F0} min — re-triggering as re-run",
                id, elapsed.TotalMinutes);

            session.Status = BulkSopImportStatus.Validated;
            session.IsRerun = true;
            session.ProcessingStartedAt = null;
            session.UpdatedAt = DateTime.UtcNow;
            session.UpdatedBy = _currentUser.UserId;
            await _context.SaveChangesAsync(ct);
        }
        else if (session.Status != BulkSopImportStatus.Validated)
        {
            return BadRequest(Result.Fail<BulkSopImportConfirmResponseDto>(
                $"Session is '{session.Status}' and cannot be confirmed. Only Validated sessions can be started."));
        }

        // Enqueue via the concrete class so Hangfire reads [AutomaticRetry(Attempts=0)] from
        // the class-level attribute. Enqueueing via the interface loses class attributes.
        var jobId = BackgroundJob.Enqueue<BulkSopImportJob>(
            j => j.ExecuteAsync(id, CancellationToken.None));

        _logger.LogInformation(
            "BulkSopImport confirm: session {SessionId} enqueued as Hangfire job {JobId}", id, jobId);

        return Ok(Result.Ok(new BulkSopImportConfirmResponseDto(jobId)));
    }

    // ── Status ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Get the current status, validation summary, and per-PDF processing results for a session.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken ct)
    {
        var session = await GetOwnedSessionAsync(id, ct);
        if (session is null)
            return NotFound(Result.Fail<BulkSopImportSessionStatusDto>("Import session not found."));

        var validationSummary = session.ValidationResultJson is null
            ? null
            : ToValidationSummary(
                JsonSerializer.Deserialize<BulkSopImportValidationResult>(session.ValidationResultJson, JsonOpts)!);

        var processingSummary = session.ProcessingResultJson is null
            ? null
            : ToProcessingSummary(
                JsonSerializer.Deserialize<BulkSopImportProcessingResult>(session.ProcessingResultJson, JsonOpts)!);

        return Ok(Result.Ok(new BulkSopImportSessionStatusDto(
            session.Id,
            session.Status.ToString(),
            session.UploadedAt,
            validationSummary,
            processingSummary)));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Task<BulkSopImportSession?> GetOwnedSessionAsync(Guid id, CancellationToken ct)
        => _context.BulkSopImportSessions
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);

    private static BulkSopImportValidationSummaryDto ToValidationSummary(BulkSopImportValidationResult r) =>
        new(
            r.TotalPdfCount,
            r.TotalUncompressedBytes,
            r.Files.Select(f => f.FileName).ToList(),
            r.IgnoredEntryNames);

    private static BulkSopImportProcessingSummaryDto ToProcessingSummary(BulkSopImportProcessingResult r) =>
        new(
            r.TotalAttempted,
            r.SucceededCount,
            r.FailedCount,
            r.AlreadyExistedCount,
            r.Items.Select(o => new BulkSopImportOutcomeDto(
                o.ItemIndex,
                o.FileName,
                o.Status.ToString(),
                o.ToolboxTalkId,
                o.ToolboxTalkTitle,
                o.FailureReason,
                o.Warning)).ToList());
}
