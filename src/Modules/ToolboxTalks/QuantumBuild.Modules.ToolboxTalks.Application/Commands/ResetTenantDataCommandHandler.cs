using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands;

public class ResetTenantDataCommandHandler(
    IToolboxTalksDbContext dbContext,
    IR2StorageService r2StorageService,
    ILogger<ResetTenantDataCommandHandler> logger) : IRequestHandler<ResetTenantDataCommand, Result>
{
    public async Task<Result> Handle(ResetTenantDataCommand request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId;
        logger.LogWarning("Starting tenant data reset for TenantId={TenantId}", tenantId);

        var db = (DbContext)dbContext;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Step 1: ScheduledTalkSectionProgress, ScheduledTalkQuizAttempt
                var progressDeleted = await dbContext.ScheduledTalkSectionProgress
                    .IgnoreQueryFilters()
                    .Where(p => p.ScheduledTalk.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ScheduledTalkSectionProgress rows for TenantId={TenantId}", progressDeleted, tenantId);

                var quizAttemptsDeleted = await dbContext.ScheduledTalkQuizAttempts
                    .IgnoreQueryFilters()
                    .Where(a => a.ScheduledTalk.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ScheduledTalkQuizAttempt rows for TenantId={TenantId}", quizAttemptsDeleted, tenantId);

                // Step 2: ScheduledTalkCompletion
                var completionsDeleted = await dbContext.ScheduledTalkCompletions
                    .IgnoreQueryFilters()
                    .Where(c => c.ScheduledTalk.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ScheduledTalkCompletion rows for TenantId={TenantId}", completionsDeleted, tenantId);

                // Step 3: ScheduledTalk
                var scheduledTalksDeleted = await dbContext.ScheduledTalks
                    .IgnoreQueryFilters()
                    .Where(st => st.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ScheduledTalk rows for TenantId={TenantId}", scheduledTalksDeleted, tenantId);

                // Step 4: ToolboxTalkScheduleAssignment
                var scheduleAssignmentsDeleted = await dbContext.ToolboxTalkScheduleAssignments
                    .IgnoreQueryFilters()
                    .Where(sa => sa.Schedule.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ToolboxTalkScheduleAssignment rows for TenantId={TenantId}", scheduleAssignmentsDeleted, tenantId);

                // Step 5: ToolboxTalkSchedule
                var schedulesDeleted = await dbContext.ToolboxTalkSchedules
                    .IgnoreQueryFilters()
                    .Where(s => s.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ToolboxTalkSchedule rows for TenantId={TenantId}", schedulesDeleted, tenantId);

                // Step 6: ToolboxTalkCourseAssignment
                var courseAssignmentsDeleted = await dbContext.ToolboxTalkCourseAssignments
                    .IgnoreQueryFilters()
                    .Where(ca => ca.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ToolboxTalkCourseAssignment rows for TenantId={TenantId}", courseAssignmentsDeleted, tenantId);

                // Step 7: ToolboxTalkCertificate
                var certificatesDeleted = await dbContext.ToolboxTalkCertificates
                    .IgnoreQueryFilters()
                    .Where(c => c.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ToolboxTalkCertificate rows for TenantId={TenantId}", certificatesDeleted, tenantId);

                // Step 8: TranslationValidationResult
                var validationResultsDeleted = await dbContext.TranslationValidationResults
                    .IgnoreQueryFilters()
                    .Where(r => r.ValidationRun.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} TranslationValidationResult rows for TenantId={TenantId}", validationResultsDeleted, tenantId);

                // Step 9: ValidationRegulatoryScore
                var regulatoryScoresDeleted = await dbContext.ValidationRegulatoryScores
                    .IgnoreQueryFilters()
                    .Where(s => s.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ValidationRegulatoryScore rows for TenantId={TenantId}", regulatoryScoresDeleted, tenantId);

                // Step 10: TranslationValidationRun
                var validationRunsDeleted = await dbContext.TranslationValidationRuns
                    .IgnoreQueryFilters()
                    .Where(r => r.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} TranslationValidationRun rows for TenantId={TenantId}", validationRunsDeleted, tenantId);

                // Step 11: SubtitleTranslation
                var subtitleTranslationsDeleted = await dbContext.SubtitleTranslations
                    .IgnoreQueryFilters()
                    .Where(st => st.ProcessingJob.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} SubtitleTranslation rows for TenantId={TenantId}", subtitleTranslationsDeleted, tenantId);

                // Step 12: SubtitleProcessingJob
                var subtitleJobsDeleted = await dbContext.SubtitleProcessingJobs
                    .IgnoreQueryFilters()
                    .Where(j => j.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} SubtitleProcessingJob rows for TenantId={TenantId}", subtitleJobsDeleted, tenantId);

                // Step 13: ContentCreationSession
                var sessionsDeleted = await dbContext.ContentCreationSessions
                    .IgnoreQueryFilters()
                    .Where(s => s.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ContentCreationSession rows for TenantId={TenantId}", sessionsDeleted, tenantId);

                // Step 14: ToolboxTalkSlideTranslation
                var slideTranslationsDeleted = await dbContext.ToolboxTalkSlideTranslations
                    .IgnoreQueryFilters()
                    .Where(st => st.Slide.ToolboxTalk.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ToolboxTalkSlideTranslation rows for TenantId={TenantId}", slideTranslationsDeleted, tenantId);

                // Step 15: ToolboxTalkSlide, ToolboxTalkSlideshowTranslation, ToolboxTalkVideoTranslation, ToolboxTalkTranslation
                var slidesDeleted = await dbContext.ToolboxTalkSlides
                    .IgnoreQueryFilters()
                    .Where(s => s.ToolboxTalk.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ToolboxTalkSlide rows for TenantId={TenantId}", slidesDeleted, tenantId);

                var slideshowTranslationsDeleted = await dbContext.ToolboxTalkSlideshowTranslations
                    .IgnoreQueryFilters()
                    .Where(st => st.ToolboxTalk.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ToolboxTalkSlideshowTranslation rows for TenantId={TenantId}", slideshowTranslationsDeleted, tenantId);

                var videoTranslationsDeleted = await dbContext.ToolboxTalkVideoTranslations
                    .IgnoreQueryFilters()
                    .Where(vt => vt.ToolboxTalk.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ToolboxTalkVideoTranslation rows for TenantId={TenantId}", videoTranslationsDeleted, tenantId);

                var translationsDeleted = await dbContext.ToolboxTalkTranslations
                    .IgnoreQueryFilters()
                    .Where(t => t.ToolboxTalk.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ToolboxTalkTranslation rows for TenantId={TenantId}", translationsDeleted, tenantId);

                // Step 16: ToolboxTalkSection, ToolboxTalkQuestion
                var sectionsDeleted = await dbContext.ToolboxTalkSections
                    .IgnoreQueryFilters()
                    .Where(s => s.ToolboxTalk.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ToolboxTalkSection rows for TenantId={TenantId}", sectionsDeleted, tenantId);

                var questionsDeleted = await dbContext.ToolboxTalkQuestions
                    .IgnoreQueryFilters()
                    .Where(q => q.ToolboxTalk.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ToolboxTalkQuestion rows for TenantId={TenantId}", questionsDeleted, tenantId);

                // Step 17: ToolboxTalkCourseItem
                var courseItemsDeleted = await dbContext.ToolboxTalkCourseItems
                    .IgnoreQueryFilters()
                    .Where(ci => ci.ToolboxTalk.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ToolboxTalkCourseItem rows for TenantId={TenantId}", courseItemsDeleted, tenantId);

                // Step 18: RegulatoryRequirementMapping
                var mappingsDeleted = await dbContext.RegulatoryRequirementMappings
                    .IgnoreQueryFilters()
                    .Where(m => m.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} RegulatoryRequirementMapping rows for TenantId={TenantId}", mappingsDeleted, tenantId);

                // Step 19: AiUsageLog, AiUsageSummary
                var aiLogsDeleted = await dbContext.AiUsageLogs
                    .IgnoreQueryFilters()
                    .Where(l => l.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} AiUsageLog rows for TenantId={TenantId}", aiLogsDeleted, tenantId);

                var aiSummariesDeleted = await dbContext.AiUsageSummaries
                    .IgnoreQueryFilters()
                    .Where(s => s.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} AiUsageSummary rows for TenantId={TenantId}", aiSummariesDeleted, tenantId);

                // Step 20: ToolboxTalkCourseTranslation
                var courseTranslationsDeleted = await dbContext.ToolboxTalkCourseTranslations
                    .IgnoreQueryFilters()
                    .Where(ct => ct.Course.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ToolboxTalkCourseTranslation rows for TenantId={TenantId}", courseTranslationsDeleted, tenantId);

                // Step 21: ToolboxTalkCourse
                var coursesDeleted = await dbContext.ToolboxTalkCourses
                    .IgnoreQueryFilters()
                    .Where(c => c.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ToolboxTalkCourse rows for TenantId={TenantId}", coursesDeleted, tenantId);

                // Step 22: ToolboxTalk
                var talksDeleted = await dbContext.ToolboxTalks
                    .IgnoreQueryFilters()
                    .Where(t => t.TenantId == tenantId)
                    .ExecuteDeleteAsync(cancellationToken);
                logger.LogInformation("Deleted {Count} ToolboxTalk rows for TenantId={TenantId}", talksDeleted, tenantId);

                await transaction.CommitAsync(cancellationToken);
                logger.LogWarning("Tenant data reset transaction committed for TenantId={TenantId}", tenantId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[ResetTenantData] Reset failed for TenantId={TenantId}", tenantId);
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });

        // Step 23: R2 storage cleanup (after transaction commits)
        try
        {
            await r2StorageService.DeleteAllTenantFilesAsync(tenantId, cancellationToken);
            logger.LogInformation("R2 storage files deleted for TenantId={TenantId}", tenantId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete R2 storage files for TenantId={TenantId}. Files can be cleaned up later.", tenantId);
        }

        return Result.Ok();
    }
}
