using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;

/// <summary>
/// Database context interface for the Toolbox Talks module
/// </summary>
public interface IToolboxTalksDbContext
{
    DbSet<ToolboxTalk> ToolboxTalks { get; }
    DbSet<ToolboxTalkSection> ToolboxTalkSections { get; }
    DbSet<ToolboxTalkQuestion> ToolboxTalkQuestions { get; }
    DbSet<ToolboxTalkTranslation> ToolboxTalkTranslations { get; }
    DbSet<ToolboxTalkVideoTranslation> ToolboxTalkVideoTranslations { get; }
    DbSet<ToolboxTalkSchedule> ToolboxTalkSchedules { get; }
    DbSet<ToolboxTalkScheduleAssignment> ToolboxTalkScheduleAssignments { get; }
    DbSet<ScheduledTalk> ScheduledTalks { get; }
    DbSet<ScheduledTalkSectionProgress> ScheduledTalkSectionProgress { get; }
    DbSet<ScheduledTalkQuizAttempt> ScheduledTalkQuizAttempts { get; }
    DbSet<ScheduledTalkCompletion> ScheduledTalkCompletions { get; }
    DbSet<ToolboxTalkSettings> ToolboxTalkSettings { get; }

    // Course entities
    DbSet<ToolboxTalkCourse> ToolboxTalkCourses { get; }
    DbSet<ToolboxTalkCourseItem> ToolboxTalkCourseItems { get; }
    DbSet<ToolboxTalkCourseTranslation> ToolboxTalkCourseTranslations { get; }
    DbSet<ToolboxTalkCourseAssignment> ToolboxTalkCourseAssignments { get; }

    // Certificate entities
    DbSet<ToolboxTalkCertificate> ToolboxTalkCertificates { get; }

    // Slide entities
    DbSet<ToolboxTalkSlide> ToolboxTalkSlides { get; }
    DbSet<ToolboxTalkSlideTranslation> ToolboxTalkSlideTranslations { get; }
    DbSet<ToolboxTalkSlideshowTranslation> ToolboxTalkSlideshowTranslations { get; }

    // Subtitle processing entities
    DbSet<SubtitleProcessingJob> SubtitleProcessingJobs { get; }
    DbSet<SubtitleTranslation> SubtitleTranslations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
