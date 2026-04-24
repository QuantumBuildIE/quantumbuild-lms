using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QRCoder;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.API.Controllers;

// ── Request records ──────────────────────────────────────────────────────────

public record CreateQrLocationRequest(
    string Name,
    string? Description,
    string? Address);

public record UpdateQrLocationRequest(
    string Name,
    string? Description,
    string? Address,
    bool IsActive);

public record CreateQrCodeRequest(
    string Name,
    Guid? ToolboxTalkId,
    Guid? CourseId,
    string ContentMode);

public record UpdateQrCodeRequest(
    string Name,
    Guid? ToolboxTalkId,
    Guid? CourseId,
    string ContentMode,
    bool IsActive);

// ── Response records ─────────────────────────────────────────────────────────

public record QrLocationDto(
    Guid Id,
    string Name,
    string? Description,
    string? Address,
    bool IsActive,
    DateTime CreatedAt,
    int QrCodeCount);

public record QrCodeDto(
    Guid Id,
    Guid QrLocationId,
    Guid? ToolboxTalkId,
    string? TalkTitle,
    Guid? CourseId,
    string? CourseTitle,
    string Name,
    string ContentMode,
    string CodeToken,
    bool IsActive,
    string? QrImageUrl);

public record QrCodePublicDto(
    string CodeToken,
    string LocationName,
    Guid? TalkId,
    string? TalkTitle,
    Guid? CourseId,
    string? CourseTitle,
    string ContentMode);

public record QrSessionListItemDto(
    Guid Id,
    Guid SessionToken,
    string EmployeeName,
    string LocationName,
    string? TalkTitle,
    string ContentMode,
    string Language,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int? Score);

public record QrSessionsByLocationItem(string LocationName, int Count);
public record QrSessionsByLanguageItem(string Language, int Count);

public record QrSessionSummaryDto(
    int TotalSessions,
    int CompletedSessions,
    int AbandonedSessions,
    int ActiveSessions,
    double? AverageScore,
    IList<QrSessionsByLocationItem> SessionsByLocation,
    IList<QrSessionsByLanguageItem> SessionsByLanguage);

// ── Controller ───────────────────────────────────────────────────────────────

[ApiController]
[Route("api/qr-locations")]
[Authorize(Policy = "Learnings.View")]
public class QrLocationController : ControllerBase
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IR2StorageService _r2;
    private readonly IConfiguration _configuration;
    private readonly ILogger<QrLocationController> _logger;

    public QrLocationController(
        IToolboxTalksDbContext dbContext,
        ICurrentUserService currentUserService,
        IR2StorageService r2,
        IConfiguration configuration,
        ILogger<QrLocationController> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _r2 = r2;
        _configuration = configuration;
        _logger = logger;
    }

    // ── Locations ─────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetLocations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbContext.QrLocations.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(x => x.Name.Contains(search) || (x.Address != null && x.Address.Contains(search)));

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderBy(x => x.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new QrLocationDto(
                    x.Id,
                    x.Name,
                    x.Description,
                    x.Address,
                    x.IsActive,
                    x.CreatedAt,
                    x.QrCodes.Count(c => !c.IsDeleted)))
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                items,
                totalCount,
                pageNumber = page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving QR locations");
            return StatusCode(500, Result.Fail("Error retrieving QR locations"));
        }
    }

    [HttpPost]
    [Authorize(Policy = "Learnings.Admin")]
    public async Task<IActionResult> CreateLocation(
        [FromBody] CreateQrLocationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var location = new QrLocation
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                Address = request.Address,
                IsActive = true
            };

            _dbContext.QrLocations.Add(location);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var dto = new QrLocationDto(location.Id, location.Name, location.Description,
                location.Address, location.IsActive, location.CreatedAt, 0);

            return CreatedAtAction(nameof(GetLocations), new { }, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating QR location");
            return StatusCode(500, Result.Fail("Error creating QR location"));
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Learnings.Admin")]
    public async Task<IActionResult> UpdateLocation(
        Guid id,
        [FromBody] UpdateQrLocationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var location = await _dbContext.QrLocations
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (location == null) return NotFound();

            location.Name = request.Name;
            location.Description = request.Description;
            location.Address = request.Address;
            location.IsActive = request.IsActive;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating QR location {Id}", id);
            return StatusCode(500, Result.Fail("Error updating QR location"));
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Learnings.Admin")]
    public async Task<IActionResult> DeleteLocation(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var location = await _dbContext.QrLocations
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (location == null) return NotFound();

            location.IsDeleted = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting QR location {Id}", id);
            return StatusCode(500, Result.Fail("Error deleting QR location"));
        }
    }

    // ── QR Codes ─────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/codes")]
    public async Task<IActionResult> GetCodes(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var locationExists = await _dbContext.QrLocations
                .AnyAsync(x => x.Id == id, cancellationToken);

            if (!locationExists) return NotFound();

            var codes = await _dbContext.QrCodes
                .Where(x => x.QrLocationId == id)
                .Include(x => x.ToolboxTalk)
                .Include(x => x.Course)
                .OrderBy(x => x.Name)
                .Select(x => new QrCodeDto(
                    x.Id,
                    x.QrLocationId,
                    x.ToolboxTalkId,
                    x.ToolboxTalk != null ? x.ToolboxTalk.Title : null,
                    x.CourseId,
                    x.Course != null ? x.Course.Title : null,
                    x.Name,
                    x.ContentMode.ToString(),
                    x.CodeToken,
                    x.IsActive,
                    x.QrImageUrl))
                .ToListAsync(cancellationToken);

            return Ok(codes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving QR codes for location {Id}", id);
            return StatusCode(500, Result.Fail("Error retrieving QR codes"));
        }
    }

    [HttpPost("{id:guid}/codes")]
    [Authorize(Policy = "Learnings.Admin")]
    public async Task<IActionResult> CreateCode(
        Guid id,
        [FromBody] CreateQrCodeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var locationExists = await _dbContext.QrLocations
                .AnyAsync(x => x.Id == id, cancellationToken);

            if (!locationExists) return NotFound(new { message = "Location not found" });

            if (request.ToolboxTalkId.HasValue && request.CourseId.HasValue)
                return BadRequest(new { message = "ToolboxTalkId and CourseId are mutually exclusive." });

            if (!Enum.TryParse<ContentMode>(request.ContentMode, true, out var contentMode))
                return BadRequest(new { message = $"Invalid ContentMode: {request.ContentMode}" });

            var codeToken = Guid.NewGuid().ToString("N");
            var appBaseUrl = _configuration["AppSettings:BaseUrl"]?.TrimEnd('/')
                ?? "https://rascorweb-production.up.railway.app";
            var qrUrl = $"{appBaseUrl}/qr/{codeToken}";

            var pngBytes = GenerateQrPng(qrUrl);
            var tenantId = _currentUserService.TenantId;

            string? imageUrl = null;
            if (pngBytes != null)
            {
                var uploadResult = await _r2.UploadQrCodeImageAsync(tenantId, codeToken, pngBytes, cancellationToken);
                if (uploadResult.Success)
                    imageUrl = uploadResult.PublicUrl;
                else
                    _logger.LogWarning("QR image upload failed for token {Token}: {Error}", codeToken, uploadResult.ErrorMessage);
            }

            var code = new QrCode
            {
                Id = Guid.NewGuid(),
                QrLocationId = id,
                ToolboxTalkId = request.ToolboxTalkId,
                CourseId = request.CourseId,
                Name = request.Name,
                ContentMode = contentMode,
                CodeToken = codeToken,
                IsActive = true,
                QrImageUrl = imageUrl
            };

            _dbContext.QrCodes.Add(code);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var talkTitle = request.ToolboxTalkId.HasValue
                ? await _dbContext.ToolboxTalks
                    .Where(t => t.Id == request.ToolboxTalkId.Value)
                    .Select(t => t.Title)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;

            var courseTitle = request.CourseId.HasValue
                ? await _dbContext.ToolboxTalkCourses
                    .Where(c => c.Id == request.CourseId.Value)
                    .Select(c => c.Title)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;

            var dto = new QrCodeDto(code.Id, code.QrLocationId, code.ToolboxTalkId,
                talkTitle, code.CourseId, courseTitle, code.Name, code.ContentMode.ToString(),
                code.CodeToken, code.IsActive, code.QrImageUrl);

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating QR code for location {Id}", id);
            return StatusCode(500, Result.Fail("Error creating QR code"));
        }
    }

    [HttpPut("{id:guid}/codes/{codeId:guid}")]
    [Authorize(Policy = "Learnings.Admin")]
    public async Task<IActionResult> UpdateCode(
        Guid id,
        Guid codeId,
        [FromBody] UpdateQrCodeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var code = await _dbContext.QrCodes
                .FirstOrDefaultAsync(x => x.Id == codeId && x.QrLocationId == id, cancellationToken);

            if (code == null) return NotFound();

            if (request.ToolboxTalkId.HasValue && request.CourseId.HasValue)
                return BadRequest(new { message = "ToolboxTalkId and CourseId are mutually exclusive." });

            if (!Enum.TryParse<ContentMode>(request.ContentMode, true, out var contentMode))
                return BadRequest(new { message = $"Invalid ContentMode: {request.ContentMode}" });

            code.Name = request.Name;
            code.ToolboxTalkId = request.ToolboxTalkId;
            code.CourseId = request.CourseId;
            code.ContentMode = contentMode;
            code.IsActive = request.IsActive;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating QR code {CodeId}", codeId);
            return StatusCode(500, Result.Fail("Error updating QR code"));
        }
    }

    [HttpDelete("{id:guid}/codes/{codeId:guid}")]
    [Authorize(Policy = "Learnings.Admin")]
    public async Task<IActionResult> DeleteCode(
        Guid id,
        Guid codeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var code = await _dbContext.QrCodes
                .FirstOrDefaultAsync(x => x.Id == codeId && x.QrLocationId == id, cancellationToken);

            if (code == null) return NotFound();

            code.IsDeleted = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting QR code {CodeId}", codeId);
            return StatusCode(500, Result.Fail("Error deleting QR code"));
        }
    }

    // ── Sessions ──────────────────────────────────────────────────────────────

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(
        [FromQuery] Guid? employeeId = null,
        [FromQuery] Guid? qrCodeId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var query = _dbContext.QrSessions
                .Where(s => s.TenantId == tenantId)
                .AsQueryable();

            if (employeeId.HasValue)
                query = query.Where(s => s.EmployeeId == employeeId.Value);

            if (qrCodeId.HasValue)
                query = query.Where(s => s.QrCodeId == qrCodeId.Value);

            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<QrSessionStatus>(status, true, out var parsedStatus))
                query = query.Where(s => s.Status == parsedStatus);

            if (from.HasValue)
                query = query.Where(s => s.StartedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(s => s.StartedAt <= to.Value);

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(s => s.StartedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new QrSessionListItemDto(
                    s.Id,
                    s.SessionToken,
                    s.Employee.FirstName + " " + s.Employee.LastName,
                    s.QrCode.QrLocation.Name,
                    s.QrCode.ToolboxTalk != null ? s.QrCode.ToolboxTalk.Title : null,
                    s.ContentMode.ToString(),
                    s.Language,
                    s.Status.ToString(),
                    s.StartedAt,
                    s.CompletedAt,
                    s.Score))
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                items,
                totalCount,
                pageNumber = page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving QR sessions");
            return StatusCode(500, Result.Fail("Error retrieving QR sessions"));
        }
    }

    [HttpGet("sessions/summary")]
    public async Task<IActionResult> GetSessionsSummary(CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var sessionData = await _dbContext.QrSessions
                .Where(s => s.TenantId == tenantId)
                .Select(s => new { s.Status, s.Score })
                .ToListAsync(cancellationToken);

            var total = sessionData.Count;
            var completed = sessionData.Count(s => s.Status == QrSessionStatus.Completed);
            var abandoned = sessionData.Count(s => s.Status == QrSessionStatus.Abandoned);
            var active = sessionData.Count(s => s.Status == QrSessionStatus.Active);
            var completedScores = sessionData
                .Where(s => s.Status == QrSessionStatus.Completed && s.Score.HasValue)
                .Select(s => (double)s.Score!.Value)
                .ToList();
            double? avgScore = completedScores.Count > 0 ? completedScores.Average() : null;

            var byLocation = await _dbContext.QrSessions
                .Where(s => s.TenantId == tenantId)
                .Select(s => new { LocationName = s.QrCode.QrLocation.Name })
                .GroupBy(s => s.LocationName)
                .Select(g => new QrSessionsByLocationItem(g.Key, g.Count()))
                .OrderByDescending(x => x.Count)
                .ToListAsync(cancellationToken);

            var byLanguage = await _dbContext.QrSessions
                .Where(s => s.TenantId == tenantId)
                .GroupBy(s => s.Language)
                .Select(g => new QrSessionsByLanguageItem(g.Key, g.Count()))
                .OrderByDescending(x => x.Count)
                .ToListAsync(cancellationToken);

            return Ok(new QrSessionSummaryDto(
                total, completed, abandoned, active, avgScore, byLocation, byLanguage));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving QR session summary");
            return StatusCode(500, Result.Fail("Error retrieving QR session summary"));
        }
    }

    // ── Public endpoint ───────────────────────────────────────────────────────

    [HttpGet("codes/{codeToken}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCodePublic(string codeToken, CancellationToken cancellationToken)
    {
        try
        {
            var code = await _dbContext.QrCodes
                .IgnoreQueryFilters()
                .Where(x => !x.IsDeleted && x.IsActive && x.CodeToken == codeToken)
                .Include(x => x.QrLocation)
                .Include(x => x.ToolboxTalk)
                .Include(x => x.Course)
                .FirstOrDefaultAsync(cancellationToken);

            if (code == null) return NotFound();

            var dto = new QrCodePublicDto(
                code.CodeToken,
                code.QrLocation.Name,
                code.ToolboxTalkId,
                code.ToolboxTalk?.Title,
                code.CourseId,
                code.Course?.Title,
                code.ContentMode.ToString());

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving public QR code info for token {Token}", codeToken);
            return StatusCode(500, Result.Fail("Error retrieving QR code info"));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static byte[]? GenerateQrPng(string url)
    {
        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            return qrCode.GetGraphic(10);
        }
        catch
        {
            return null;
        }
    }
}
