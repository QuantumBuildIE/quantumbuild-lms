using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Abstractions;
using QuantumBuild.Core.Application.Features.BulkSopImport;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.GenerateToolboxTalkQuiz;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.InitialiseToolboxTalk;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.ParseToolboxTalkContent;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Core.Infrastructure.Services;

namespace QuantumBuild.Core.Infrastructure.Jobs;

/// <summary>
/// Reads the queued PDF entries from a BulkSopImportSession's ZIP and creates one Draft
/// learning per PDF, reusing the new learning-wizard's own initialise -> parse -> quiz-generate
/// command chain (InitialiseToolboxTalkCommand, ParseToolboxTalkContentCommand,
/// GenerateToolboxTalkQuizCommand) so generated content is identical to what a human using the
/// wizard would produce. Translation is out of scope — learnings land as Draft with no target
/// languages set, exactly as a wizard user would leave them before starting translation.
///
/// Partial-failure model: each PDF is independent. A per-item failure is recorded and the job
/// continues to the next PDF. The session transitions to Completed once the job finishes (even
/// with some failed items). Only a job-level exception causes Failed status.
///
/// AutomaticRetry is disabled: the session is already in Processing when the job runs, and a
/// retry would attempt to re-create learnings that may have already succeeded.
/// </summary>
[AutomaticRetry(Attempts = 0)]
public class BulkSopImportJob : IBulkSopImportJob
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private readonly ICoreDbContext _context;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IR2StorageService _storageService;
    private readonly ILogger<BulkSopImportJob> _logger;

    public BulkSopImportJob(
        ICoreDbContext context,
        IServiceScopeFactory scopeFactory,
        IR2StorageService storageService,
        ILogger<BulkSopImportJob> logger)
    {
        _context = context;
        _scopeFactory = scopeFactory;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid sessionId, CancellationToken ct)
    {
        _logger.LogInformation("BulkSopImportJob starting for session {SessionId}", sessionId);

        // Bypass tenant query filter — Hangfire has no HTTP context so ICurrentUserService
        // returns no tenant. Filter explicitly by ID instead.
        var session = await _context.BulkSopImportSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == sessionId && !s.IsDeleted, ct);

        if (session is null)
        {
            _logger.LogError("BulkSopImportJob: session {SessionId} not found", sessionId);
            return;
        }

        if (session.Status != BulkSopImportStatus.Validated)
        {
            _logger.LogWarning(
                "BulkSopImportJob: session {SessionId} is in status {Status}, expected Validated — aborting",
                sessionId, session.Status);
            return;
        }

        var tenantId = session.TenantId;

        session.Status = BulkSopImportStatus.Processing;
        session.ProcessingStartedAt = DateTimeOffset.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        var outcomes = new List<BulkSopImportItemOutcome>();

        try
        {
            if (string.IsNullOrWhiteSpace(session.ValidationResultJson))
                throw new InvalidOperationException("Session has no ValidationResultJson — cannot process.");

            var validationResult = JsonSerializer.Deserialize<BulkSopImportValidationResult>(
                session.ValidationResultJson, JsonOpts)
                ?? throw new InvalidOperationException("ValidationResultJson deserialized to null.");

            var zipBytes = await _storageService.DownloadFileAsync(session.ZipR2Key, ct)
                ?? throw new InvalidOperationException($"ZIP file {session.ZipR2Key} was not found in storage.");

            _logger.LogInformation(
                "BulkSopImportJob: session {SessionId} — {Count} PDFs to process for tenant {TenantId}",
                sessionId, validationResult.Files.Count, tenantId);

            using var zipStream = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);

            foreach (var file in validationResult.Files)
            {
                ct.ThrowIfCancellationRequested();
                var outcome = await ProcessItemAsync(archive, file, tenantId, sessionId, session.IsRerun, ct);
                outcomes.Add(outcome);
            }

            var processingResult = BuildResult(outcomes);
            session.ProcessingResultJson = JsonSerializer.Serialize(processingResult, JsonOpts);
            session.Status = BulkSopImportStatus.Completed;
            session.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "BulkSopImportJob: session {SessionId} completed — " +
                "{Succeeded} succeeded, {AlreadyExisted} already-existed, {Failed} failed",
                sessionId, processingResult.SucceededCount, processingResult.AlreadyExistedCount,
                processingResult.FailedCount);

            // R2 cleanup — deletes the ZIP and every re-uploaded PDF under this session's prefix
            // in one call, now the session is permanently Completed. Non-fatal on failure.
            try
            {
                await _storageService.DeleteSessionFilesAsync(tenantId, sessionId, ct);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx,
                    "BulkSopImportJob: failed to delete session files for session {SessionId} — leaving for manual cleanup",
                    sessionId);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "BulkSopImportJob: job-level failure for session {SessionId}", sessionId);

            if (outcomes.Count > 0)
            {
                var partialResult = BuildResult(outcomes);
                session.ProcessingResultJson = JsonSerializer.Serialize(partialResult, JsonOpts);
            }

            // ZIP is intentionally left in R2 on job-level failure for investigation.
            session.Status = BulkSopImportStatus.Failed;
            session.UpdatedAt = DateTime.UtcNow;

            try { await _context.SaveChangesAsync(ct); }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx,
                    "BulkSopImportJob: could not persist Failed status for session {SessionId}", sessionId);
            }

            throw;
        }
    }

    /// <summary>
    /// Processes a single PDF entry in its own DI scope so a DbContext failure on one item
    /// cannot leave a bad entity in the change tracker and cascade into the next item.
    /// </summary>
    private async Task<BulkSopImportItemOutcome> ProcessItemAsync(
        ZipArchive archive,
        BulkSopFileEntry file,
        Guid tenantId,
        Guid sessionId,
        bool isRerun,
        CancellationToken ct)
    {
        try
        {
            var entry = archive.GetEntry(file.EntryName);
            if (entry is null)
            {
                return Failed(file, "The file could not be found in the archive.");
            }

            var title = DeriveTitle(file.FileName, file.ItemIndex);

            await using var itemScope = _scopeFactory.CreateAsyncScope();

            // Hangfire has no HttpContext, so ICurrentUserService.TenantId would otherwise
            // resolve to Guid.Empty inside this scope — the EF Core tenant query filter on
            // ToolboxTalks (and everything else the wizard handlers below touch) would then
            // match nothing, even for a row this very job just created with the correct
            // explicit TenantId. Setting the tenant here, before anything else is resolved
            // from this scope, makes the ambient filter resolve correctly for the unmodified
            // wizard command handlers — mirroring the explicit-tenant approach
            // TranslationWorkflowService uses for the same Hangfire/HttpContext gap, applied
            // via scoped DI context instead of a method parameter since the shared handlers
            // must not change.
            itemScope.ServiceProvider.GetRequiredService<IJobTenantContextAccessor>().TenantId = tenantId;

            var sender = itemScope.ServiceProvider.GetRequiredService<ISender>();
            var toolboxTalksDb = itemScope.ServiceProvider.GetRequiredService<IToolboxTalksDbContext>();

            var existing = await toolboxTalksDb.ToolboxTalks
                .IgnoreQueryFilters()
                .Where(t => t.TenantId == tenantId && t.Title == title && !t.IsDeleted)
                .Select(t => new { t.Id, t.Title })
                .FirstOrDefaultAsync(ct);

            if (existing is not null)
            {
                if (isRerun)
                {
                    _logger.LogInformation(
                        "BulkSopImportJob: item {Index} ('{Title}') re-run — a learning with this title " +
                        "already exists from the previous run (session {SessionId})",
                        file.ItemIndex, title, sessionId);

                    return new BulkSopImportItemOutcome
                    {
                        ItemIndex = file.ItemIndex,
                        FileName = file.FileName,
                        Status = BulkSopImportItemStatus.AlreadyExisted,
                        ToolboxTalkId = existing.Id,
                        ToolboxTalkTitle = existing.Title
                    };
                }

                return Failed(file, $"A learning titled '{title}' already exists.");
            }

            await using var entryStream = entry.Open();
            await using var seekableStream = new MemoryStream();
            await entryStream.CopyToAsync(seekableStream, ct);
            seekableStream.Position = 0;

            var uploadResult = await _storageService.UploadSessionFileAsync(
                tenantId, sessionId, seekableStream, $"{file.ItemIndex:D3}_{file.FileName}", "application/pdf", ct);

            if (!uploadResult.Success || string.IsNullOrEmpty(uploadResult.PublicUrl))
            {
                return Failed(file, $"Failed to store the PDF: {uploadResult.ErrorMessage ?? "unknown storage error"}");
            }

            var talk = await sender.Send(new InitialiseToolboxTalkCommand
            {
                TenantId = tenantId,
                Title = title,
                InputMode = InputMode.Pdf,
                SourceFileUrl = uploadResult.PublicUrl,
                SourceFileName = file.FileName,
                SourceFileType = "application/pdf"
            }, ct);

            var parseResult = await sender.Send(
                new ParseToolboxTalkContentCommand(talk.Id, tenantId, UserId: null), ct);

            if (!parseResult.Success)
            {
                var reason = string.Join("; ", parseResult.Errors);
                _logger.LogWarning(
                    "BulkSopImportJob: item {Index} ('{FileName}') failed to parse for session {SessionId}: {Reason}",
                    file.ItemIndex, file.FileName, sessionId, reason);
                return Failed(file, reason);
            }

            var quizResult = await sender.Send(
                new GenerateToolboxTalkQuizCommand(talk.Id, tenantId, UserId: null), ct);

            string? warning = null;
            if (!quizResult.Success)
            {
                warning = $"Quiz generation failed: {string.Join("; ", quizResult.Errors)}. " +
                    "The learning was created with sections but no quiz — quiz can be generated manually.";
                _logger.LogWarning(
                    "BulkSopImportJob: item {Index} ('{Title}') quiz generation failed for session {SessionId}: {Reason}",
                    file.ItemIndex, title, sessionId, warning);
            }

            return new BulkSopImportItemOutcome
            {
                ItemIndex = file.ItemIndex,
                FileName = file.FileName,
                Status = BulkSopImportItemStatus.Succeeded,
                ToolboxTalkId = talk.Id,
                ToolboxTalkTitle = title,
                Warning = warning
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "BulkSopImportJob: unexpected error on item {Index} ('{FileName}') for session {SessionId}",
                file.ItemIndex, file.FileName, sessionId);

            return Failed(file, ex.Message);
        }
    }

    private static BulkSopImportItemOutcome Failed(BulkSopFileEntry file, string reason) => new()
    {
        ItemIndex = file.ItemIndex,
        FileName = file.FileName,
        Status = BulkSopImportItemStatus.Failed,
        FailureReason = reason
    };

    /// <summary>
    /// Derives a learning title from a PDF file name: strips the extension, replaces
    /// underscores/dashes with spaces, and collapses whitespace. Falls back to a positional
    /// placeholder if the result is empty (e.g. a file literally named ".pdf").
    /// </summary>
    private static string DeriveTitle(string fileName, int itemIndex)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var cleaned = WhitespaceRegex.Replace(baseName.Replace('_', ' ').Replace('-', ' '), " ").Trim();

        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = $"SOP Import {itemIndex + 1}";

        return cleaned.Length > 200 ? cleaned[..200] : cleaned;
    }

    private static BulkSopImportProcessingResult BuildResult(List<BulkSopImportItemOutcome> outcomes) => new()
    {
        TotalAttempted = outcomes.Count,
        SucceededCount = outcomes.Count(o => o.Status == BulkSopImportItemStatus.Succeeded),
        FailedCount = outcomes.Count(o => o.Status == BulkSopImportItemStatus.Failed),
        AlreadyExistedCount = outcomes.Count(o => o.Status == BulkSopImportItemStatus.AlreadyExisted),
        Items = outcomes
    };
}
