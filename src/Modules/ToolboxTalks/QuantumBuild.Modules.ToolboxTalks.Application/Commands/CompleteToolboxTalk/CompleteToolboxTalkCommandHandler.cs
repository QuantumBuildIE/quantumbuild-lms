using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.CompleteToolboxTalk;

public class CompleteToolboxTalkCommandHandler : IRequestHandler<CompleteToolboxTalkCommand, ScheduledTalkCompletionDto>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICoreDbContext _coreDbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICourseProgressService _courseProgressService;
    private readonly IRefresherSchedulingService _refresherSchedulingService;
    private readonly ICertificateGenerationService _certificateService;
    private readonly ILogger<CompleteToolboxTalkCommandHandler> _logger;

    public CompleteToolboxTalkCommandHandler(
        IToolboxTalksDbContext dbContext,
        ICoreDbContext coreDbContext,
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor,
        ICourseProgressService courseProgressService,
        IRefresherSchedulingService refresherSchedulingService,
        ICertificateGenerationService certificateService,
        ILogger<CompleteToolboxTalkCommandHandler> logger)
    {
        _dbContext = dbContext;
        _coreDbContext = coreDbContext;
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
        _courseProgressService = courseProgressService;
        _refresherSchedulingService = refresherSchedulingService;
        _certificateService = certificateService;
        _logger = logger;
    }

    public async Task<ScheduledTalkCompletionDto> Handle(CompleteToolboxTalkCommand request, CancellationToken cancellationToken)
    {
        // Get the current user's employee record
        var employee = await _coreDbContext.Employees
            .FirstOrDefaultAsync(e => e.UserId == _currentUserService.UserId &&
                                      e.TenantId == _currentUserService.TenantId &&
                                      !e.IsDeleted,
                                 cancellationToken);

        if (employee == null)
        {
            throw new InvalidOperationException("No employee record found for the current user.");
        }

        // Get the scheduled talk with all related data
        var scheduledTalk = await _dbContext.ScheduledTalks
            .Include(st => st.SectionProgress)
            .Include(st => st.QuizAttempts)
            .Include(st => st.Completion)
            .Include(st => st.ToolboxTalk)
                .ThenInclude(t => t.Sections)
            .FirstOrDefaultAsync(st => st.Id == request.ScheduledTalkId &&
                                       st.TenantId == _currentUserService.TenantId,
                                 cancellationToken);

        if (scheduledTalk == null)
        {
            throw new InvalidOperationException($"Scheduled talk with ID '{request.ScheduledTalkId}' not found.");
        }

        // Validate the scheduled talk belongs to the current user's employee
        if (scheduledTalk.EmployeeId != employee.Id)
        {
            throw new UnauthorizedAccessException("You are not authorized to access this scheduled talk.");
        }

        // Validate the scheduled talk is not already completed or cancelled
        if (scheduledTalk.Status == ScheduledTalkStatus.Completed)
        {
            throw new InvalidOperationException("This scheduled talk has already been completed.");
        }

        if (scheduledTalk.Status == ScheduledTalkStatus.Cancelled)
        {
            throw new InvalidOperationException("This scheduled talk has been cancelled.");
        }

        if (scheduledTalk.Completion != null)
        {
            throw new InvalidOperationException("A completion record already exists for this scheduled talk.");
        }

        // Validate all sections are read
        var allSections = scheduledTalk.ToolboxTalk.Sections.ToList();
        if (allSections.Any())
        {
            var readSections = scheduledTalk.SectionProgress.Where(p => p.IsRead).Select(p => p.SectionId).ToHashSet();
            var unreadSections = allSections.Where(s => !readSections.Contains(s.Id)).ToList();
            if (unreadSections.Any())
            {
                throw new InvalidOperationException($"You must read all sections before completing. Unread sections: {string.Join(", ", unreadSections.Select(s => s.Title))}");
            }
        }

        // If quiz is required, validate a passing attempt exists
        int? quizScore = null;
        int? quizMaxScore = null;
        bool? quizPassed = null;

        if (scheduledTalk.ToolboxTalk.RequiresQuiz)
        {
            var lastPassingAttempt = scheduledTalk.QuizAttempts
                .Where(a => a.Passed)
                .OrderByDescending(a => a.AttemptedAt)
                .FirstOrDefault();

            if (lastPassingAttempt == null)
            {
                throw new InvalidOperationException("You must pass the quiz before completing this learning.");
            }

            quizScore = lastPassingAttempt.Score;
            quizMaxScore = lastPassingAttempt.MaxScore;
            quizPassed = lastPassingAttempt.Passed;
        }

        // If video exists and completion is required, validate watch percentage
        int? videoWatchPercent = null;
        if (!string.IsNullOrEmpty(scheduledTalk.ToolboxTalk.VideoUrl))
        {
            videoWatchPercent = scheduledTalk.VideoWatchPercent;

            // Get tenant settings to check if video completion is required
            var settings = await _dbContext.ToolboxTalkSettings
                .FirstOrDefaultAsync(s => s.TenantId == _currentUserService.TenantId, cancellationToken);

            var requireVideoCompletion = settings?.RequireVideoCompletion ?? true;

            if (requireVideoCompletion)
            {
                var minimumWatchPercent = scheduledTalk.ToolboxTalk.MinimumVideoWatchPercent;
                if (scheduledTalk.VideoWatchPercent < minimumWatchPercent)
                {
                    throw new InvalidOperationException($"You must watch at least {minimumWatchPercent}% of the video. Current progress: {scheduledTalk.VideoWatchPercent}%.");
                }
            }
        }

        // Calculate total time spent from section progress
        var totalTimeSpentSeconds = scheduledTalk.SectionProgress.Sum(p => p.TimeSpentSeconds);

        // Get IP address and User Agent from HttpContext
        var httpContext = _httpContextAccessor.HttpContext;
        var ipAddress = GetClientIpAddress(httpContext);
        var userAgent = httpContext?.Request.Headers["User-Agent"].ToString();

        // Truncate userAgent if too long
        if (userAgent?.Length > 500)
        {
            userAgent = userAgent.Substring(0, 500);
        }

        var now = DateTime.UtcNow;

        // Create completion record
        var completion = new ScheduledTalkCompletion
        {
            Id = Guid.NewGuid(),
            ScheduledTalkId = scheduledTalk.Id,
            CompletedAt = now,
            TotalTimeSpentSeconds = totalTimeSpentSeconds,
            VideoWatchPercent = videoWatchPercent,
            QuizScore = quizScore,
            QuizMaxScore = quizMaxScore,
            QuizPassed = quizPassed,
            SignatureData = request.SignatureData,
            SignedAt = now,
            SignedByName = request.SignedByName,
            IPAddress = ipAddress,
            UserAgent = userAgent,
            CompletedLatitude = request.Latitude,
            CompletedLongitude = request.Longitude,
            CompletedAccuracyMeters = request.AccuracyMeters,
            CompletedLocationTimestamp = request.Latitude.HasValue ? now : null
        };

        _dbContext.ScheduledTalkCompletions.Add(completion);

        // Update scheduled talk status
        scheduledTalk.Status = ScheduledTalkStatus.Completed;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Update course assignment progress if this talk belongs to a course
        if (scheduledTalk.CourseAssignmentId.HasValue)
        {
            await _courseProgressService.UpdateProgressAsync(scheduledTalk.CourseAssignmentId.Value, cancellationToken);
        }
        else
        {
            // For standalone talks (not part of a course), schedule refresher directly
            await _refresherSchedulingService.ScheduleRefresherIfRequired(scheduledTalk, cancellationToken);

            // Generate certificate for standalone talks (course certs generated by CourseProgressService)
            try
            {
                var certificate = await _certificateService.GenerateTalkCertificateAsync(
                    scheduledTalk,
                    request.SignatureData,
                    cancellationToken);

                if (certificate != null)
                {
                    completion.CertificateUrl = certificate.PdfStoragePath;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate certificate for scheduled talk {TalkId}", scheduledTalk.Id);
                // Don't rethrow — completion should still succeed
            }
        }

        return new ScheduledTalkCompletionDto
        {
            Id = completion.Id,
            ScheduledTalkId = completion.ScheduledTalkId,
            CompletedAt = completion.CompletedAt,
            TotalTimeSpentSeconds = completion.TotalTimeSpentSeconds,
            VideoWatchPercent = completion.VideoWatchPercent,
            QuizScore = completion.QuizScore,
            QuizMaxScore = completion.QuizMaxScore,
            QuizPassed = completion.QuizPassed,
            SignatureData = completion.SignatureData,
            SignedAt = completion.SignedAt,
            SignedByName = completion.SignedByName,
            IPAddress = completion.IPAddress,
            UserAgent = completion.UserAgent,
            CertificateUrl = completion.CertificateUrl,
            CompletedLatitude = completion.CompletedLatitude,
            CompletedLongitude = completion.CompletedLongitude,
            CompletedAccuracyMeters = completion.CompletedAccuracyMeters,
            CompletedLocationTimestamp = completion.CompletedLocationTimestamp
        };
    }

    private static string? GetClientIpAddress(HttpContext? httpContext)
    {
        if (httpContext == null) return null;

        // Check for forwarded IP first (for proxies/load balancers)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP in the list (client's original IP)
            var ip = forwardedFor.Split(',').First().Trim();
            if (ip.Length <= 50) return ip;
        }

        // Fall back to remote IP address
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
        if (remoteIp?.Length <= 50) return remoteIp;

        return null;
    }
}
