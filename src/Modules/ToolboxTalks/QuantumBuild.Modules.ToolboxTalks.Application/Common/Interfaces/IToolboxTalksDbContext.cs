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

    // Translation validation entities
    DbSet<TranslationValidationRun> TranslationValidationRuns { get; }
    DbSet<TranslationValidationResult> TranslationValidationResults { get; }

    // Pipeline version audit entities
    DbSet<PipelineVersion> PipelineVersions { get; }
    DbSet<PipelineChangeRecord> PipelineChangeRecords { get; }
    DbSet<TranslationDeviation> TranslationDeviations { get; }

    // Safety glossary entities
    DbSet<SafetyGlossary> SafetyGlossaries { get; }
    DbSet<SafetyGlossaryTerm> SafetyGlossaryTerms { get; }

    // Content creation session entities
    DbSet<ContentCreationSession> ContentCreationSessions { get; }

    // Sector entities
    DbSet<Sector> Sectors { get; }
    DbSet<TenantSector> TenantSectors { get; }

    // Regulatory profile entities
    DbSet<RegulatoryBody> RegulatoryBodies { get; }
    DbSet<RegulatoryDocument> RegulatoryDocuments { get; }
    DbSet<RegulatoryProfile> RegulatoryProfiles { get; }
    DbSet<RegulatoryCriteria> RegulatoryCriteria { get; }

    // Regulatory score entities
    DbSet<ValidationRegulatoryScore> ValidationRegulatoryScores { get; }

    // Regulatory requirement entities
    DbSet<RegulatoryRequirement> RegulatoryRequirements { get; }
    DbSet<RegulatoryRequirementMapping> RegulatoryRequirementMappings { get; }

    // AI usage tracking entities
    DbSet<AiUsageLog> AiUsageLogs { get; }
    DbSet<AiUsageSummary> AiUsageSummaries { get; }

    // Corpus audit entities
    DbSet<AuditCorpus> AuditCorpora { get; }
    DbSet<AuditCorpusEntry> AuditCorpusEntries { get; }
    DbSet<CorpusRun> CorpusRuns { get; }
    DbSet<CorpusRunResult> CorpusRunResults { get; }
    DbSet<ProviderResultCache> ProviderResultCache { get; }

    // QR location entities
    DbSet<QrLocation> QrLocations { get; }
    DbSet<QrCode> QrCodes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
