using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Services;

public class CourseProgressService(
    IToolboxTalksDbContext dbContext,
    IRefresherSchedulingService refresherSchedulingService,
    ICertificateGenerationService certificateService,
    ILogger<CourseProgressService> logger) : ICourseProgressService
{
    public async Task UpdateProgressAsync(Guid courseAssignmentId, CancellationToken cancellationToken = default)
    {
        var assignment = await dbContext.ToolboxTalkCourseAssignments
            .Include(a => a.ScheduledTalks)
            .Include(a => a.Course)
                .ThenInclude(c => c.CourseItems)
            .FirstOrDefaultAsync(a => a.Id == courseAssignmentId, cancellationToken);

        if (assignment == null)
        {
            logger.LogWarning("Course assignment {CourseAssignmentId} not found for progress update", courseAssignmentId);
            return;
        }

        // Already completed — no need to reprocess
        if (assignment.Status == CourseAssignmentStatus.Completed)
            return;

        var requiredTalkIds = assignment.Course.CourseItems
            .Where(ci => ci.IsRequired && !ci.IsDeleted)
            .Select(ci => ci.ToolboxTalkId)
            .ToHashSet();

        var completedRequiredCount = assignment.ScheduledTalks
            .Count(st => !st.IsDeleted
                && st.Status == ScheduledTalkStatus.Completed
                && requiredTalkIds.Contains(st.ToolboxTalkId));

        var now = DateTime.UtcNow;

        // Transition Assigned → InProgress on first completion
        if (assignment.Status == CourseAssignmentStatus.Assigned && completedRequiredCount > 0)
        {
            assignment.Status = CourseAssignmentStatus.InProgress;
            assignment.StartedAt ??= now;
            logger.LogInformation("Course assignment {CourseAssignmentId} moved to InProgress ({Completed}/{Total} required talks)",
                courseAssignmentId, completedRequiredCount, requiredTalkIds.Count);
        }

        // Transition to Completed when all required talks are done
        if (completedRequiredCount >= requiredTalkIds.Count && requiredTalkIds.Count > 0)
        {
            assignment.Status = CourseAssignmentStatus.Completed;
            assignment.CompletedAt = now;
            logger.LogInformation("Course assignment {CourseAssignmentId} completed — all {Total} required talks done",
                courseAssignmentId, requiredTalkIds.Count);
        }

        var saved = await dbContext.SaveChangesAsync(cancellationToken);

        // Schedule refresher and generate certificate if course was just completed
        if (assignment.Status == CourseAssignmentStatus.Completed)
        {
            await refresherSchedulingService.ScheduleRefresherIfRequired(assignment, cancellationToken);

            // Generate course certificate
            try
            {
                // Get signature from the last completed talk
                var lastCompletedTalk = assignment.ScheduledTalks
                    .Where(st => st.Status == ScheduledTalkStatus.Completed && !st.IsDeleted)
                    .OrderByDescending(st => st.UpdatedAt)
                    .FirstOrDefault();

                string? signature = null;
                if (lastCompletedTalk != null)
                {
                    // Load the completion record to get the signature
                    var completion = await dbContext.ScheduledTalkCompletions
                        .FirstOrDefaultAsync(c => c.ScheduledTalkId == lastCompletedTalk.Id, cancellationToken);
                    signature = completion?.SignatureData;
                }

                await certificateService.GenerateCourseCertificateAsync(
                    assignment,
                    signature,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate certificate for course assignment {AssignmentId}", assignment.Id);
                // Don't rethrow — completion should still succeed
            }
        }
        logger.LogDebug("Course progress update saved {RowCount} rows for assignment {CourseAssignmentId}",
            saved, courseAssignmentId);
    }
}
