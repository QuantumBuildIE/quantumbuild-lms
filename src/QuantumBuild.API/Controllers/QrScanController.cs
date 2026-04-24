using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Abstractions;
using QuantumBuild.Core.Application.Features.TenantSettings;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.API.Controllers;

// ── Request records ──────────────────────────────────────────────────────────

public record VerifyPinRequest(string EmployeePin);
public record QrCompleteRequest(int? Score, string? SignatureData);

// ── Response records ─────────────────────────────────────────────────────────

public record QrVerifyPinResponse(
    string SessionToken,
    Guid EmployeeId,
    string EmployeeName,
    string PreferredLanguage,
    Guid? TalkId,
    string ContentMode,
    string LocationName);

public record QrSessionSectionDto(
    Guid SectionId,
    int SectionNumber,
    string Title,
    string Content,
    bool RequiresAcknowledgment);

public record QrSessionQuestionDto(
    Guid QuestionId,
    int QuestionNumber,
    string QuestionText,
    string QuestionType,
    List<string>? Options,
    int? CorrectOptionIndex,
    int Points);

public record QrSessionTalkDto(
    Guid Id,
    string Title,
    string? Description,
    string? VideoUrl,
    bool RequiresQuiz,
    int PassingScore,
    List<QrSessionSectionDto> Sections,
    List<QrSessionQuestionDto> Questions);

public record QrSessionDto(
    string SessionToken,
    string Status,
    string ContentMode,
    string Language,
    DateTimeOffset StartedAt,
    string EmployeeName,
    string LocationName,
    QrSessionTalkDto? Talk);

// ── Controller ───────────────────────────────────────────────────────────────

[ApiController]
[Route("api/qr")]
[AllowAnonymous]
public class QrScanController : ControllerBase
{
    private readonly IToolboxTalksDbContext _db;
    private readonly ICoreDbContext _coreDb;
    private readonly IEmployeePinService _pinService;
    private readonly IPasswordHasher<Employee> _passwordHasher;
    private readonly ILogger<QrScanController> _logger;

    public QrScanController(
        IToolboxTalksDbContext db,
        ICoreDbContext coreDb,
        IEmployeePinService pinService,
        IPasswordHasher<Employee> passwordHasher,
        ILogger<QrScanController> logger)
    {
        _db = db;
        _coreDb = coreDb;
        _pinService = pinService;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    // ── POST /{codeToken}/verify-pin ─────────────────────────────────────────

    [HttpPost("{codeToken}/verify-pin")]
    public async Task<IActionResult> VerifyPin(
        string codeToken,
        [FromBody] VerifyPinRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var code = await _db.QrCodes
                .IgnoreQueryFilters()
                .Where(x => !x.IsDeleted && x.IsActive && x.CodeToken == codeToken)
                .Include(x => x.QrLocation)
                .FirstOrDefaultAsync(cancellationToken);

            if (code == null)
                return NotFound(new { message = "QR code not found or inactive." });

            if (code.ToolboxTalkId == null)
                return BadRequest(new { message = "This QR code has no talk assigned." });

            var tenantId = code.TenantId;

            var enabledSetting = await _coreDb.TenantSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    s => s.TenantId == tenantId && s.Key == TenantSettingKeys.QrLocationTrainingEnabled,
                    cancellationToken);

            if (enabledSetting?.Value != "true")
                return BadRequest(new { message = "QR location training is not enabled." });

            var employees = await _coreDb.Employees
                .IgnoreQueryFilters()
                .Where(e => !e.IsDeleted && e.IsActive && e.TenantId == tenantId && e.QrPinIsSet)
                .ToListAsync(cancellationToken);

            // Phase 1: find matching employee by hash only (no side effects on other employees)
            Employee? matched = null;
            foreach (var emp in employees)
            {
                if (string.IsNullOrEmpty(emp.QrPin)) continue;
                var check = _passwordHasher.VerifyHashedPassword(emp, emp.QrPin, request.EmployeePin);
                if (check == PasswordVerificationResult.Success ||
                    check == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    matched = emp;
                    break;
                }
            }

            if (matched == null)
                return Unauthorized(new { status = "failed", attemptsRemaining = 0 });

            // Phase 2: call VerifyPinAsync on matched employee only (handles lockout tracking)
            var result = await _pinService.VerifyPinAsync(matched, request.EmployeePin, cancellationToken);

            if (result.Status == PinVerificationStatus.Locked)
                return StatusCode(423, new { status = "locked", lockedUntil = result.LockedUntil });

            if (result.Status == PinVerificationStatus.Failed)
                return Unauthorized(new { status = "failed", attemptsRemaining = result.AttemptsRemaining });

            var session = new QrSession
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EmployeeId = matched.Id,
                QrCodeId = code.Id,
                SessionToken = Guid.NewGuid(),
                Language = matched.PreferredLanguage ?? "en",
                ContentMode = code.ContentMode,
                StartedAt = DateTimeOffset.UtcNow,
                Status = QrSessionStatus.Active
            };

            _db.QrSessions.Add(session);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new QrVerifyPinResponse(
                session.SessionToken.ToString(),
                matched.Id,
                matched.FullName,
                matched.PreferredLanguage ?? "en",
                code.ToolboxTalkId,
                code.ContentMode.ToString(),
                code.QrLocation.Name));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying PIN for QR code {Token}", codeToken);
            return StatusCode(500, Result.Fail("Error processing PIN verification."));
        }
    }

    // ── GET /session/{sessionToken} ──────────────────────────────────────────

    [HttpGet("session/{sessionToken:guid}")]
    public async Task<IActionResult> GetSession(
        Guid sessionToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await _db.QrSessions
                .IgnoreQueryFilters()
                .Where(s => !s.IsDeleted && s.SessionToken == sessionToken)
                .Include(s => s.QrCode)
                    .ThenInclude(c => c.QrLocation)
                .Include(s => s.Employee)
                .FirstOrDefaultAsync(cancellationToken);

            if (session == null)
                return NotFound();

            if (session.Status == QrSessionStatus.Completed || session.Status == QrSessionStatus.Abandoned)
                return StatusCode(410, new { message = "Session has ended." });

            if (session.QrCode.ToolboxTalkId == null)
                return BadRequest(new { message = "No talk assigned to this QR code." });

            var talk = await _db.ToolboxTalks
                .IgnoreQueryFilters()
                .Where(t => !t.IsDeleted && t.Id == session.QrCode.ToolboxTalkId.Value)
                .Include(t => t.Sections.Where(s => !s.IsDeleted).OrderBy(s => s.SectionNumber))
                .Include(t => t.Questions.Where(q => !q.IsDeleted).OrderBy(q => q.QuestionNumber))
                .Include(t => t.Translations)
                .FirstOrDefaultAsync(cancellationToken);

            if (talk == null)
                return NotFound(new { message = "Talk not found." });

            var lang = session.Language;
            var translation = talk.Translations?.FirstOrDefault(t => t.LanguageCode == lang);

            var translatedSections = ParseTranslatedSections(translation?.TranslatedSections);
            var translatedQuestions = ParseTranslatedQuestions(translation?.TranslatedQuestions);

            var sections = talk.Sections.Select(s =>
            {
                var ts = translatedSections?.FirstOrDefault(x => x.SectionId == s.Id);
                return new QrSessionSectionDto(
                    s.Id,
                    s.SectionNumber,
                    ts?.Title ?? s.Title,
                    ts?.Content ?? s.Content,
                    s.RequiresAcknowledgment);
            }).ToList();

            var questions = talk.Questions.Select(q =>
            {
                var tq = translatedQuestions?.FirstOrDefault(x => x.QuestionId == q.Id);
                return new QrSessionQuestionDto(
                    q.Id,
                    q.QuestionNumber,
                    tq?.QuestionText ?? q.QuestionText,
                    q.QuestionType.ToString(),
                    tq?.Options ?? ParseOptions(q.Options),
                    q.CorrectOptionIndex,
                    q.Points);
            }).ToList();

            var talkDto = new QrSessionTalkDto(
                talk.Id,
                translation?.TranslatedTitle ?? talk.Title,
                translation?.TranslatedDescription ?? talk.Description,
                talk.VideoUrl,
                talk.RequiresQuiz,
                talk.PassingScore ?? 80,
                sections,
                questions);

            var dto = new QrSessionDto(
                session.SessionToken.ToString(),
                session.Status.ToString(),
                session.ContentMode.ToString(),
                session.Language,
                session.StartedAt,
                session.Employee.FullName,
                session.QrCode.QrLocation.Name,
                talkDto);

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving QR session {Token}", sessionToken);
            return StatusCode(500, Result.Fail("Error retrieving session."));
        }
    }

    // ── POST /session/{sessionToken}/complete ────────────────────────────────

    [HttpPost("session/{sessionToken:guid}/complete")]
    public async Task<IActionResult> CompleteSession(
        Guid sessionToken,
        [FromBody] QrCompleteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await _db.QrSessions
                .IgnoreQueryFilters()
                .Where(s => !s.IsDeleted && s.SessionToken == sessionToken)
                .Include(s => s.QrCode)
                .Include(s => s.Employee)
                .FirstOrDefaultAsync(cancellationToken);

            if (session == null)
                return NotFound();

            if (session.Status != QrSessionStatus.Active)
                return BadRequest(new { message = "Session is not active." });

            if (session.QrCode.ToolboxTalkId == null)
                return BadRequest(new { message = "No talk assigned to this QR code." });

            var now = DateTimeOffset.UtcNow;

            var scheduledTalk = new ScheduledTalk
            {
                Id = Guid.NewGuid(),
                TenantId = session.TenantId,
                ToolboxTalkId = session.QrCode.ToolboxTalkId.Value,
                EmployeeId = session.EmployeeId,
                LanguageCode = session.Language,
                RequiredDate = session.StartedAt.UtcDateTime,
                DueDate = now.UtcDateTime,
                Status = ScheduledTalkStatus.Completed
            };

            _db.ScheduledTalks.Add(scheduledTalk);

            var ipAddress = GetClientIp();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
            if (userAgent.Length > 500) userAgent = userAgent[..500];

            var completion = new ScheduledTalkCompletion
            {
                Id = Guid.NewGuid(),
                ScheduledTalkId = scheduledTalk.Id,
                CompletedAt = now.UtcDateTime,
                TotalTimeSpentSeconds = (int)(now - session.StartedAt).TotalSeconds,
                QuizScore = request.Score,
                SignatureData = request.SignatureData ?? string.Empty,
                SignedAt = now.UtcDateTime,
                SignedByName = session.Employee.FullName,
                IPAddress = ipAddress,
                UserAgent = userAgent
            };

            _db.ScheduledTalkCompletions.Add(completion);

            session.Status = QrSessionStatus.Completed;
            session.CompletedAt = now;
            session.SignedOffAt = now;
            session.Score = request.Score;

            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new { message = "Session completed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing QR session {Token}", sessionToken);
            return StatusCode(500, Result.Fail("Error completing session."));
        }
    }

    // ── POST /session/{sessionToken}/abandon ─────────────────────────────────

    [HttpPost("session/{sessionToken:guid}/abandon")]
    public async Task<IActionResult> AbandonSession(
        Guid sessionToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await _db.QrSessions
                .IgnoreQueryFilters()
                .Where(s => !s.IsDeleted && s.SessionToken == sessionToken)
                .FirstOrDefaultAsync(cancellationToken);

            if (session == null)
                return NotFound();

            if (session.Status != QrSessionStatus.Active)
                return BadRequest(new { message = "Session is not active." });

            session.Status = QrSessionStatus.Abandoned;
            session.CompletedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new { message = "Session abandoned." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error abandoning QR session {Token}", sessionToken);
            return StatusCode(500, Result.Fail("Error abandoning session."));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string? GetClientIp()
    {
        var forwarded = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
        {
            var ip = forwarded.Split(',').First().Trim();
            return ip.Length <= 50 ? ip : null;
        }
        var remote = HttpContext.Connection.RemoteIpAddress?.ToString();
        return remote?.Length <= 50 ? remote : null;
    }

    private static List<TranslatedSectionData>? ParseTranslatedSections(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<List<TranslatedSectionData>>(json); }
        catch { return null; }
    }

    private static List<TranslatedQuestionData>? ParseTranslatedQuestions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<List<TranslatedQuestionData>>(json); }
        catch { return null; }
    }

    private static List<string>? ParseOptions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<List<string>>(json); }
        catch { return null; }
    }

    private sealed class TranslatedSectionData
    {
        public Guid SectionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private sealed class TranslatedQuestionData
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public List<string>? Options { get; set; }
    }
}
