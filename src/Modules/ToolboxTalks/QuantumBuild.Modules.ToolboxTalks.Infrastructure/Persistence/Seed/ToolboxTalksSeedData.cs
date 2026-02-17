using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds test data for the Toolbox Talks module
/// </summary>
public static class ToolboxTalksSeedData
{
    /// <summary>
    /// Default tenant ID for QUANTUMBUILD (must match Core.Infrastructure.Persistence.DataSeeder)
    /// </summary>
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // Predefined GUIDs for Toolbox Talks (using 'a' prefix for talks)
    private static readonly Guid HotWeatherTalkId = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private static readonly Guid FireSafetyTalkId = Guid.Parse("a2222222-2222-2222-2222-222222222222");
    private static readonly Guid ManualHandlingTalkId = Guid.Parse("a3333333-3333-3333-3333-333333333333");
    private static readonly Guid PpeTalkId = Guid.Parse("a4444444-4444-4444-4444-444444444444");
    private static readonly Guid ElectricalSafetyTalkId = Guid.Parse("a5555555-5555-5555-5555-555555555555");

    // Predefined GUIDs for Schedules (using 'b' prefix for schedules)
    private static readonly Guid CompletedScheduleId = Guid.Parse("b1111111-1111-1111-1111-111111111111");
    private static readonly Guid ActiveScheduleId = Guid.Parse("b2222222-2222-2222-2222-222222222222");
    private static readonly Guid FutureScheduleId = Guid.Parse("b3333333-3333-3333-3333-333333333333");

    // Employee IDs (from Core seeder)
    private static readonly Guid[] EmployeeIds =
    [
        Guid.Parse("e1111111-1111-1111-1111-111111111111"), // Michael O'Brien
        Guid.Parse("e2222222-2222-2222-2222-222222222222"), // Sean Murphy
        Guid.Parse("e3333333-3333-3333-3333-333333333333"), // Aoife Walsh
        Guid.Parse("e4444444-4444-4444-4444-444444444444"), // Patrick Kelly
        Guid.Parse("e5555555-5555-5555-5555-555555555555"), // Ciara Ryan
        Guid.Parse("e6666666-6666-6666-6666-666666666666"), // Declan Byrne
        Guid.Parse("e7777777-7777-7777-7777-777777777777"), // Niamh Doyle
        Guid.Parse("e8888888-8888-8888-8888-888888888888")  // Brian McCarthy
    ];

    /// <summary>
    /// Seed all Toolbox Talks module data
    /// </summary>
    public static async Task SeedAsync(DbContext context, ILogger logger)
    {
        await SeedToolboxTalkSettingsAsync(context, logger);
        var talks = await SeedToolboxTalksAsync(context, logger);
        var schedules = await SeedToolboxTalkSchedulesAsync(context, logger, talks);
        logger.LogInformation("Skipping scheduled talks seed data (not needed for fresh deployment)");
    }

    private static async Task SeedToolboxTalkSettingsAsync(DbContext context, ILogger logger)
    {
        if (await context.Set<ToolboxTalkSettings>().IgnoreQueryFilters().AnyAsync(s => s.TenantId == DefaultTenantId))
        {
            logger.LogInformation("Toolbox Talk settings already exist, skipping");
            return;
        }

        var settings = new ToolboxTalkSettings
        {
            Id = Guid.NewGuid(),
            TenantId = DefaultTenantId,
            DefaultDueDays = 7,
            ReminderFrequencyDays = 1,
            MaxReminders = 5,
            EscalateAfterReminders = 3,
            RequireVideoCompletion = true,
            DefaultPassingScore = 80,
            EnableTranslation = false,
            EnableVideoDubbing = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };

        await context.Set<ToolboxTalkSettings>().AddAsync(settings);
        await context.SaveChangesAsync();
        logger.LogInformation("Created Learning settings");
    }

    private static async Task<List<ToolboxTalk>> SeedToolboxTalksAsync(DbContext context, ILogger logger)
    {
        if (await context.Set<ToolboxTalk>().IgnoreQueryFilters().AnyAsync(t => t.TenantId == DefaultTenantId))
        {
            logger.LogInformation("Learnings already exist, skipping");
            return await context.Set<ToolboxTalk>()
                .IgnoreQueryFilters()
                .Where(t => t.TenantId == DefaultTenantId)
                .Include(t => t.Sections)
                .Include(t => t.Questions)
                .ToListAsync();
        }

        var talks = new List<ToolboxTalk>();
        var sections = new List<ToolboxTalkSection>();
        var questions = new List<ToolboxTalkQuestion>();

        // 1. Working in Hot Weather
        var hotWeatherTalk = CreateToolboxTalk(
            HotWeatherTalkId,
            "Working in Hot Weather",
            "Essential safety guidance for working safely during hot weather conditions on construction sites. Covers heat-related illness prevention, hydration, appropriate PPE, and emergency response procedures.",
            ToolboxTalkFrequency.Monthly,
            true,
            75);
        talks.Add(hotWeatherTalk);

        sections.AddRange(CreateHotWeatherSections(HotWeatherTalkId));
        questions.AddRange(CreateHotWeatherQuestions(HotWeatherTalkId));

        // 2. Fire Safety and Evacuation
        var fireSafetyTalk = CreateToolboxTalk(
            FireSafetyTalkId,
            "Fire Safety and Evacuation",
            "Comprehensive fire safety training covering prevention, extinguisher use, alarm response, evacuation procedures, and assembly point protocols for construction site environments.",
            ToolboxTalkFrequency.Annually,
            true,
            80);
        talks.Add(fireSafetyTalk);

        sections.AddRange(CreateFireSafetySections(FireSafetyTalkId));
        questions.AddRange(CreateFireSafetyQuestions(FireSafetyTalkId));

        // 3. Manual Handling Basics (with video)
        var manualHandlingTalk = CreateToolboxTalk(
            ManualHandlingTalkId,
            "Manual Handling Basics",
            "Training on proper manual handling techniques to prevent musculoskeletal injuries. Includes video demonstration of correct lifting techniques and equipment usage.",
            ToolboxTalkFrequency.Once,
            true,
            70,
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            VideoSource.YouTube,
            90);
        talks.Add(manualHandlingTalk);

        sections.AddRange(CreateManualHandlingSections(ManualHandlingTalkId));
        questions.AddRange(CreateManualHandlingQuestions(ManualHandlingTalkId));

        // 4. PPE Requirements on Site (no quiz)
        var ppeTalk = CreateToolboxTalk(
            PpeTalkId,
            "PPE Requirements on Site",
            "Overview of mandatory Personal Protective Equipment requirements for all construction site personnel. Covers hard hats, high-visibility clothing, safety footwear, and hand protection.",
            ToolboxTalkFrequency.Once,
            false,
            null);
        talks.Add(ppeTalk);

        sections.AddRange(CreatePpeSections(PpeTalkId));

        // 5. Electrical Safety Awareness
        var electricalSafetyTalk = CreateToolboxTalk(
            ElectricalSafetyTalkId,
            "Electrical Safety Awareness",
            "Critical safety training on electrical hazards in construction environments. Covers identification of hazards, safe working practices, isolation procedures, and emergency response.",
            ToolboxTalkFrequency.Annually,
            true,
            90);
        talks.Add(electricalSafetyTalk);

        sections.AddRange(CreateElectricalSafetySections(ElectricalSafetyTalkId));
        questions.AddRange(CreateElectricalSafetyQuestions(ElectricalSafetyTalkId));

        await context.Set<ToolboxTalk>().AddRangeAsync(talks);
        await context.SaveChangesAsync();

        await context.Set<ToolboxTalkSection>().AddRangeAsync(sections);
        await context.SaveChangesAsync();

        await context.Set<ToolboxTalkQuestion>().AddRangeAsync(questions);
        await context.SaveChangesAsync();

        logger.LogInformation("Created {TalkCount} Learnings with {SectionCount} sections and {QuestionCount} questions",
            talks.Count, sections.Count, questions.Count);

        // Reload with navigation properties
        return await context.Set<ToolboxTalk>()
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == DefaultTenantId)
            .Include(t => t.Sections)
            .Include(t => t.Questions)
            .ToListAsync();
    }

    private static async Task<List<ToolboxTalkSchedule>> SeedToolboxTalkSchedulesAsync(DbContext context, ILogger logger, List<ToolboxTalk> talks)
    {
        if (await context.Set<ToolboxTalkSchedule>().IgnoreQueryFilters().AnyAsync(s => s.TenantId == DefaultTenantId))
        {
            logger.LogInformation("Learning schedules already exist, skipping");
            return await context.Set<ToolboxTalkSchedule>()
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == DefaultTenantId)
                .ToListAsync();
        }

        var today = DateTime.UtcNow.Date;
        var schedules = new List<ToolboxTalkSchedule>();
        var assignments = new List<ToolboxTalkScheduleAssignment>();

        // 1. Completed schedule (past) - Fire Safety completed last week
        var completedSchedule = new ToolboxTalkSchedule
        {
            Id = CompletedScheduleId,
            TenantId = DefaultTenantId,
            ToolboxTalkId = FireSafetyTalkId,
            ScheduledDate = today.AddDays(-7),
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = true,
            Status = ToolboxTalkScheduleStatus.Completed,
            Notes = "Annual fire safety training - completed",
            CreatedAt = DateTime.UtcNow.AddDays(-14),
            CreatedBy = "system"
        };
        schedules.Add(completedSchedule);

        // 2. Active schedule (today) - Manual Handling
        var activeSchedule = new ToolboxTalkSchedule
        {
            Id = ActiveScheduleId,
            TenantId = DefaultTenantId,
            ToolboxTalkId = ManualHandlingTalkId,
            ScheduledDate = today,
            Frequency = ToolboxTalkFrequency.Once,
            AssignToAllEmployees = false,
            Status = ToolboxTalkScheduleStatus.Active,
            Notes = "New employee manual handling training",
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            CreatedBy = "system"
        };
        schedules.Add(activeSchedule);

        // Add specific employee assignments for active schedule (first 4 employees)
        for (int i = 0; i < 4; i++)
        {
            assignments.Add(new ToolboxTalkScheduleAssignment
            {
                Id = Guid.NewGuid(),
                ScheduleId = ActiveScheduleId,
                EmployeeId = EmployeeIds[i],
                IsProcessed = true,
                ProcessedAt = today,
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                CreatedBy = "system"
            });
        }

        // 3. Future schedule (next week) - Hot Weather
        var futureSchedule = new ToolboxTalkSchedule
        {
            Id = FutureScheduleId,
            TenantId = DefaultTenantId,
            ToolboxTalkId = HotWeatherTalkId,
            ScheduledDate = today.AddDays(7),
            Frequency = ToolboxTalkFrequency.Monthly,
            EndDate = today.AddMonths(6),
            AssignToAllEmployees = true,
            Status = ToolboxTalkScheduleStatus.Active,
            NextRunDate = today.AddDays(7),
            Notes = "Monthly hot weather safety refresher for summer months",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };
        schedules.Add(futureSchedule);

        await context.Set<ToolboxTalkSchedule>().AddRangeAsync(schedules);
        await context.SaveChangesAsync();

        await context.Set<ToolboxTalkScheduleAssignment>().AddRangeAsync(assignments);
        await context.SaveChangesAsync();

        logger.LogInformation("Created {ScheduleCount} schedules with {AssignmentCount} assignments",
            schedules.Count, assignments.Count);

        return schedules;
    }

    private static async Task SeedScheduledTalksAsync(DbContext context, ILogger logger, List<ToolboxTalk> talks, List<ToolboxTalkSchedule> schedules)
    {
        if (await context.Set<ScheduledTalk>().IgnoreQueryFilters().AnyAsync(st => st.TenantId == DefaultTenantId))
        {
            logger.LogInformation("Scheduled talks already exist, skipping");
            return;
        }

        var random = new Random(42); // Seeded for reproducibility
        var today = DateTime.UtcNow.Date;
        var scheduledTalks = new List<ScheduledTalk>();
        var sectionProgress = new List<ScheduledTalkSectionProgress>();
        var quizAttempts = new List<ScheduledTalkQuizAttempt>();
        var completions = new List<ScheduledTalkCompletion>();

        // Get talks by ID for easy lookup
        var fireSafetyTalk = talks.First(t => t.Id == FireSafetyTalkId);
        var manualHandlingTalk = talks.First(t => t.Id == ManualHandlingTalkId);

        // Create scheduled talks for the completed Fire Safety schedule (all employees completed)
        foreach (var employeeId in EmployeeIds)
        {
            var scheduledTalk = CreateScheduledTalk(
                fireSafetyTalk.Id,
                employeeId,
                CompletedScheduleId,
                today.AddDays(-7),
                today,
                ScheduledTalkStatus.Completed);
            scheduledTalks.Add(scheduledTalk);

            // All sections read
            foreach (var section in fireSafetyTalk.Sections.OrderBy(s => s.SectionNumber))
            {
                sectionProgress.Add(CreateSectionProgress(
                    scheduledTalk.Id,
                    section.Id,
                    true,
                    today.AddDays(-5).AddHours(random.Next(1, 8)),
                    random.Next(120, 300)));
            }

            // Quiz attempt - all passed
            var score = random.Next(4, 6); // 4 or 5 out of 5
            var quizAttempt = CreateQuizAttempt(
                scheduledTalk.Id,
                1,
                score,
                5,
                today.AddDays(-5).AddHours(random.Next(8, 16)));
            quizAttempts.Add(quizAttempt);

            // Completion record
            completions.Add(CreateCompletion(
                scheduledTalk.Id,
                today.AddDays(-5).AddHours(random.Next(8, 16)),
                random.Next(1200, 2400),
                null,
                score,
                5,
                true,
                GetEmployeeName(employeeId)));
        }

        // Create scheduled talks for the active Manual Handling schedule (mix of statuses)
        var statuses = new[] { ScheduledTalkStatus.Pending, ScheduledTalkStatus.InProgress, ScheduledTalkStatus.Completed, ScheduledTalkStatus.Overdue };

        for (int i = 0; i < 4; i++)
        {
            var employeeId = EmployeeIds[i];
            var status = statuses[i];
            var scheduledTalk = CreateScheduledTalk(
                manualHandlingTalk.Id,
                employeeId,
                ActiveScheduleId,
                today,
                today.AddDays(7),
                status);

            if (status == ScheduledTalkStatus.InProgress)
            {
                scheduledTalk.VideoWatchPercent = random.Next(30, 70);
            }
            else if (status == ScheduledTalkStatus.Overdue)
            {
                scheduledTalk.DueDate = today.AddDays(-1);
                scheduledTalk.RemindersSent = 3;
                scheduledTalk.LastReminderAt = today.AddDays(-1);
            }

            scheduledTalks.Add(scheduledTalk);

            // Add section progress for InProgress and Completed
            if (status == ScheduledTalkStatus.InProgress)
            {
                var sectionsToComplete = random.Next(1, manualHandlingTalk.Sections.Count);
                foreach (var section in manualHandlingTalk.Sections.OrderBy(s => s.SectionNumber).Take(sectionsToComplete))
                {
                    sectionProgress.Add(CreateSectionProgress(
                        scheduledTalk.Id,
                        section.Id,
                        true,
                        today.AddHours(random.Next(1, 4)),
                        random.Next(120, 300)));
                }
            }
            else if (status == ScheduledTalkStatus.Completed)
            {
                scheduledTalk.VideoWatchPercent = 100;

                foreach (var section in manualHandlingTalk.Sections.OrderBy(s => s.SectionNumber))
                {
                    sectionProgress.Add(CreateSectionProgress(
                        scheduledTalk.Id,
                        section.Id,
                        true,
                        today.AddHours(random.Next(1, 8)),
                        random.Next(120, 300)));
                }

                // Quiz attempt with multiple tries
                var failedAttempt = CreateQuizAttempt(
                    scheduledTalk.Id,
                    1,
                    2,
                    4,
                    today.AddHours(4));
                quizAttempts.Add(failedAttempt);

                var passedAttempt = CreateQuizAttempt(
                    scheduledTalk.Id,
                    2,
                    3,
                    4,
                    today.AddHours(5));
                quizAttempts.Add(passedAttempt);

                completions.Add(CreateCompletion(
                    scheduledTalk.Id,
                    today.AddHours(6),
                    random.Next(1800, 3600),
                    100,
                    3,
                    4,
                    true,
                    GetEmployeeName(employeeId)));
            }
        }

        await context.Set<ScheduledTalk>().AddRangeAsync(scheduledTalks);
        await context.SaveChangesAsync();

        await context.Set<ScheduledTalkSectionProgress>().AddRangeAsync(sectionProgress);
        await context.SaveChangesAsync();

        await context.Set<ScheduledTalkQuizAttempt>().AddRangeAsync(quizAttempts);
        await context.SaveChangesAsync();

        await context.Set<ScheduledTalkCompletion>().AddRangeAsync(completions);
        await context.SaveChangesAsync();

        logger.LogInformation(
            "Created {TalkCount} scheduled talks with {ProgressCount} section progress records, {AttemptCount} quiz attempts, and {CompletionCount} completions",
            scheduledTalks.Count, sectionProgress.Count, quizAttempts.Count, completions.Count);
    }

    #region Helper Methods

    private static ToolboxTalk CreateToolboxTalk(
        Guid id,
        string title,
        string description,
        ToolboxTalkFrequency frequency,
        bool requiresQuiz,
        int? passingScore,
        string? videoUrl = null,
        VideoSource videoSource = VideoSource.None,
        int minimumVideoWatchPercent = 90)
    {
        return new ToolboxTalk
        {
            Id = id,
            TenantId = DefaultTenantId,
            Title = title,
            Description = description,
            Frequency = frequency,
            VideoUrl = videoUrl,
            VideoSource = videoSource,
            MinimumVideoWatchPercent = minimumVideoWatchPercent,
            RequiresQuiz = requiresQuiz,
            PassingScore = passingScore,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };
    }

    private static ScheduledTalk CreateScheduledTalk(
        Guid toolboxTalkId,
        Guid employeeId,
        Guid? scheduleId,
        DateTime requiredDate,
        DateTime dueDate,
        ScheduledTalkStatus status)
    {
        return new ScheduledTalk
        {
            Id = Guid.NewGuid(),
            TenantId = DefaultTenantId,
            ToolboxTalkId = toolboxTalkId,
            EmployeeId = employeeId,
            ScheduleId = scheduleId,
            RequiredDate = requiredDate,
            DueDate = dueDate,
            Status = status,
            LanguageCode = "en",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };
    }

    private static ScheduledTalkSectionProgress CreateSectionProgress(
        Guid scheduledTalkId,
        Guid sectionId,
        bool isRead,
        DateTime? readAt,
        int timeSpentSeconds)
    {
        return new ScheduledTalkSectionProgress
        {
            Id = Guid.NewGuid(),
            ScheduledTalkId = scheduledTalkId,
            SectionId = sectionId,
            IsRead = isRead,
            ReadAt = readAt,
            TimeSpentSeconds = timeSpentSeconds,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };
    }

    private static ScheduledTalkQuizAttempt CreateQuizAttempt(
        Guid scheduledTalkId,
        int attemptNumber,
        int score,
        int maxScore,
        DateTime attemptedAt)
    {
        var percentage = maxScore > 0 ? (decimal)score / maxScore * 100 : 0;
        return new ScheduledTalkQuizAttempt
        {
            Id = Guid.NewGuid(),
            ScheduledTalkId = scheduledTalkId,
            AttemptNumber = attemptNumber,
            Answers = "{}",
            Score = score,
            MaxScore = maxScore,
            Percentage = percentage,
            Passed = percentage >= 70,
            AttemptedAt = attemptedAt,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };
    }

    private static ScheduledTalkCompletion CreateCompletion(
        Guid scheduledTalkId,
        DateTime completedAt,
        int totalTimeSpentSeconds,
        int? videoWatchPercent,
        int? quizScore,
        int? quizMaxScore,
        bool? quizPassed,
        string signedByName)
    {
        return new ScheduledTalkCompletion
        {
            Id = Guid.NewGuid(),
            ScheduledTalkId = scheduledTalkId,
            CompletedAt = completedAt,
            TotalTimeSpentSeconds = totalTimeSpentSeconds,
            VideoWatchPercent = videoWatchPercent,
            QuizScore = quizScore,
            QuizMaxScore = quizMaxScore,
            QuizPassed = quizPassed,
            SignatureData = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
            SignedAt = completedAt,
            SignedByName = signedByName,
            IPAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };
    }

    private static string GetEmployeeName(Guid employeeId)
    {
        var names = new Dictionary<Guid, string>
        {
            { Guid.Parse("e1111111-1111-1111-1111-111111111111"), "Michael O'Brien" },
            { Guid.Parse("e2222222-2222-2222-2222-222222222222"), "Sean Murphy" },
            { Guid.Parse("e3333333-3333-3333-3333-333333333333"), "Aoife Walsh" },
            { Guid.Parse("e4444444-4444-4444-4444-444444444444"), "Patrick Kelly" },
            { Guid.Parse("e5555555-5555-5555-5555-555555555555"), "Ciara Ryan" },
            { Guid.Parse("e6666666-6666-6666-6666-666666666666"), "Declan Byrne" },
            { Guid.Parse("e7777777-7777-7777-7777-777777777777"), "Niamh Doyle" },
            { Guid.Parse("e8888888-8888-8888-8888-888888888888"), "Brian McCarthy" }
        };
        return names.TryGetValue(employeeId, out var name) ? name : "Unknown Employee";
    }

    #endregion

    #region Section Content

    private static List<ToolboxTalkSection> CreateHotWeatherSections(Guid toolboxTalkId)
    {
        return new List<ToolboxTalkSection>
        {
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 1,
                Title = "Understanding Heat-Related Risks",
                Content = @"<h2>Understanding Heat-Related Risks</h2>
<p>Working in hot weather poses significant health risks that every construction worker must understand. Heat-related illnesses can range from mild discomfort to life-threatening emergencies.</p>

<h3>Types of Heat-Related Illness</h3>
<ul>
    <li><strong>Heat Cramps:</strong> Painful muscle spasms, usually in the legs or abdomen, caused by loss of salt through sweating</li>
    <li><strong>Heat Exhaustion:</strong> A more serious condition characterized by heavy sweating, weakness, cold/pale/clammy skin, fast/weak pulse, nausea, and possible fainting</li>
    <li><strong>Heat Stroke:</strong> A medical emergency where body temperature rises above 40°C (104°F). Symptoms include hot/red/dry skin, rapid pulse, confusion, and loss of consciousness</li>
</ul>

<h3>Risk Factors</h3>
<p>Certain factors increase your risk of heat-related illness:</p>
<ul>
    <li>High air temperature and humidity</li>
    <li>Direct sun exposure without shade</li>
    <li>Heavy physical work</li>
    <li>Wearing PPE that restricts heat loss</li>
    <li>Poor physical fitness or underlying health conditions</li>
    <li>Dehydration or alcohol consumption the night before</li>
    <li>Lack of acclimatization to hot conditions</li>
</ul>

<div class='warning'>
    <strong>⚠️ Important:</strong> Heat stroke is a medical emergency. If you suspect someone has heat stroke, call emergency services immediately and begin cooling them down while waiting for help.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 2,
                Title = "Staying Hydrated",
                Content = @"<h2>Staying Hydrated</h2>
<p>Proper hydration is your first line of defense against heat-related illness. In hot conditions, you can lose up to 1 liter of sweat per hour during heavy work.</p>

<h3>Hydration Guidelines</h3>
<ul>
    <li><strong>Drink regularly:</strong> Consume at least 200-300ml of water every 15-20 minutes, even if you don't feel thirsty</li>
    <li><strong>Start hydrated:</strong> Begin your shift well-hydrated by drinking plenty of fluids the evening before and morning of work</li>
    <li><strong>Monitor urine color:</strong> Pale yellow indicates good hydration; dark yellow or amber suggests dehydration</li>
    <li><strong>Replace electrolytes:</strong> For extended work periods, consider sports drinks or electrolyte supplements to replace lost salts</li>
</ul>

<h3>What to Avoid</h3>
<ul>
    <li><strong>Caffeine:</strong> Coffee, tea, and energy drinks can increase dehydration</li>
    <li><strong>Alcohol:</strong> Avoid alcohol the night before working in heat - it significantly impacts your body's ability to regulate temperature</li>
    <li><strong>Sugary drinks:</strong> High-sugar beverages can slow fluid absorption</li>
    <li><strong>Heavy meals:</strong> Large meals increase body heat during digestion</li>
</ul>

<h3>Water Station Protocol</h3>
<p>On QUANTUMBUILD sites, water stations are provided at multiple locations. You should:</p>
<ul>
    <li>Know where all water stations are located on your site</li>
    <li>Report any issues with water supply immediately to your supervisor</li>
    <li>Use individual cups or personal water bottles to prevent contamination</li>
</ul>

<div class='tip'>
    <strong>💡 Pro Tip:</strong> Set a timer on your phone to remind you to drink every 15-20 minutes during hot weather work.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 3,
                Title = "Appropriate PPE and Clothing",
                Content = @"<h2>Appropriate PPE and Clothing</h2>
<p>While PPE is essential for safety, it can also trap heat. Understanding how to dress appropriately while maintaining protection is crucial for hot weather work.</p>

<h3>Clothing Recommendations</h3>
<ul>
    <li><strong>Loose-fitting garments:</strong> Allow air circulation to help cool the body</li>
    <li><strong>Light colors:</strong> Reflect sunlight and absorb less heat than dark colors</li>
    <li><strong>Breathable fabrics:</strong> Cotton and moisture-wicking materials help sweat evaporate</li>
    <li><strong>Long sleeves:</strong> Light, loose long sleeves can actually protect from sun and keep you cooler than bare arms</li>
</ul>

<h3>Sun Protection</h3>
<ul>
    <li><strong>Hard hat with brim/neck shade:</strong> Use attachable neck shades where available</li>
    <li><strong>Sunscreen:</strong> Apply SPF 30+ to exposed skin, reapply every 2 hours</li>
    <li><strong>Safety sunglasses:</strong> UV-rated safety glasses protect eyes from glare and UV damage</li>
    <li><strong>Cooling accessories:</strong> Cooling towels and bandanas can help lower body temperature</li>
</ul>

<h3>PPE Considerations</h3>
<p>Standard PPE must still be worn, but consider these adaptations:</p>
<ul>
    <li>Vented hard hats allow better air circulation</li>
    <li>Lightweight high-visibility vests instead of heavy jackets where regulations permit</li>
    <li>Breathable safety gloves for tasks requiring hand protection</li>
    <li>Take more frequent breaks when wearing full PPE in heat</li>
</ul>

<div class='warning'>
    <strong>⚠️ Never compromise safety:</strong> All required PPE must still be worn. If heat is making it unsafe to work with required PPE, stop work and inform your supervisor.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 4,
                Title = "Recognizing Emergency Signs",
                Content = @"<h2>Recognizing Emergency Signs</h2>
<p>Quick recognition and response to heat-related emergencies can save lives. Learn to identify warning signs in yourself and your colleagues.</p>

<h3>Warning Signs - Heat Exhaustion</h3>
<ul>
    <li>Heavy sweating with cold, pale, clammy skin</li>
    <li>Fast, weak pulse</li>
    <li>Nausea or vomiting</li>
    <li>Muscle cramps</li>
    <li>Tiredness and weakness</li>
    <li>Dizziness or headache</li>
    <li>Fainting</li>
</ul>

<h3>Warning Signs - Heat Stroke (EMERGENCY)</h3>
<ul>
    <li>High body temperature (40°C/104°F or higher)</li>
    <li>Hot, red, dry, or damp skin</li>
    <li>Rapid, strong pulse</li>
    <li>Confusion or altered mental state</li>
    <li>Slurred speech</li>
    <li>Loss of consciousness</li>
</ul>

<h3>What To Do</h3>
<table>
    <tr>
        <th>Heat Exhaustion</th>
        <th>Heat Stroke</th>
    </tr>
    <tr>
        <td>
            <ul>
                <li>Move to a cool place</li>
                <li>Loosen clothing</li>
                <li>Apply cool, wet cloths</li>
                <li>Sip water slowly</li>
                <li>Get medical attention if symptoms worsen or last more than 1 hour</li>
            </ul>
        </td>
        <td>
            <ul>
                <li><strong>Call 999/112 immediately</strong></li>
                <li>Move person to cooler environment</li>
                <li>Cool them rapidly with any means available (cold water, ice packs to neck, armpits, groin)</li>
                <li>Do NOT give fluids if unconscious</li>
                <li>Stay with them until help arrives</li>
            </ul>
        </td>
    </tr>
</table>

<h3>When to Stop Work</h3>
<p>You should stop work and seek shade/rest if you experience:</p>
<ul>
    <li>Feeling unusually tired or weak</li>
    <li>Headache or dizziness</li>
    <li>Nausea</li>
    <li>Muscle cramps</li>
    <li>Excessive sweating followed by reduced sweating</li>
</ul>

<div class='warning'>
    <strong>⚠️ Remember:</strong> It's always better to take a break than to risk a medical emergency. Report any symptoms to your supervisor immediately.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            }
        };
    }

    private static List<ToolboxTalkQuestion> CreateHotWeatherQuestions(Guid toolboxTalkId)
    {
        return new List<ToolboxTalkQuestion>
        {
            new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                QuestionNumber = 1,
                QuestionText = "What are the first signs of heat exhaustion?",
                QuestionType = QuestionType.MultipleChoice,
                Options = JsonSerializer.Serialize(new[]
                {
                    "Heavy sweating, weakness, and cold/pale skin",
                    "Hot, red, dry skin and confusion",
                    "Only feeling thirsty",
                    "Sunburn and peeling skin"
                }),
                CorrectAnswer = "Heavy sweating, weakness, and cold/pale skin",
                CorrectOptionIndex = 0,
                Points = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                QuestionNumber = 2,
                QuestionText = "You should drink at least 200-300ml of water every 15-20 minutes in hot weather.",
                QuestionType = QuestionType.TrueFalse,
                Options = null,
                CorrectAnswer = "True",
                Points = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                QuestionNumber = 3,
                QuestionText = "Name one action to take if a colleague shows signs of heat stroke.",
                QuestionType = QuestionType.ShortAnswer,
                Options = null,
                CorrectAnswer = "Call emergency services",
                Points = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            }
        };
    }

    private static List<ToolboxTalkSection> CreateFireSafetySections(Guid toolboxTalkId)
    {
        return new List<ToolboxTalkSection>
        {
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 1,
                Title = "Fire Prevention",
                Content = @"<h2>Fire Prevention on Construction Sites</h2>
<p>Construction sites are particularly vulnerable to fire due to the presence of combustible materials, hot work, and temporary electrical installations. Understanding and implementing fire prevention measures is everyone's responsibility.</p>

<h3>Common Causes of Construction Site Fires</h3>
<ul>
    <li><strong>Hot work:</strong> Welding, cutting, grinding, and soldering operations</li>
    <li><strong>Electrical faults:</strong> Temporary wiring, overloaded circuits, damaged cables</li>
    <li><strong>Flammable materials:</strong> Solvents, adhesives, insulation materials, timber</li>
    <li><strong>Smoking:</strong> Discarded cigarettes in prohibited areas</li>
    <li><strong>Arson:</strong> Unauthorized access to site</li>
</ul>

<h3>Prevention Measures</h3>
<ul>
    <li>Always obtain a hot work permit before welding, cutting, or using open flames</li>
    <li>Keep work areas clean and free of combustible waste</li>
    <li>Store flammable materials in designated areas away from ignition sources</li>
    <li>Inspect electrical equipment and cables before use</li>
    <li>Smoke only in designated smoking areas</li>
    <li>Ensure fire extinguishers are in place before starting hot work</li>
    <li>Maintain clear access to fire exits and firefighting equipment</li>
</ul>

<h3>Housekeeping</h3>
<p>Good housekeeping is essential for fire prevention:</p>
<ul>
    <li>Remove rubbish and combustible waste regularly</li>
    <li>Keep escape routes clear at all times</li>
    <li>Store materials neatly, away from heat sources</li>
    <li>Report any fire hazards to your supervisor immediately</li>
</ul>

<div class='warning'>
    <strong>⚠️ Hot Work Reminder:</strong> A fire watch must be maintained for at least 30 minutes after completing any hot work. This includes checking the area for smoldering materials.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 2,
                Title = "Using Fire Extinguishers",
                Content = @"<h2>Using Fire Extinguishers</h2>
<p>Fire extinguishers are the first line of defense against small fires. Knowing how to use them correctly can prevent a small fire from becoming a major incident.</p>

<h3>Types of Fire Extinguishers</h3>
<table>
    <tr>
        <th>Type</th>
        <th>Color Band</th>
        <th>Use For</th>
    </tr>
    <tr>
        <td>Water</td>
        <td>Red</td>
        <td>Paper, wood, textiles (Class A)</td>
    </tr>
    <tr>
        <td>Foam</td>
        <td>Cream</td>
        <td>Flammable liquids (Class B) and solids</td>
    </tr>
    <tr>
        <td>CO2</td>
        <td>Black</td>
        <td>Electrical fires and flammable liquids</td>
    </tr>
    <tr>
        <td>Dry Powder</td>
        <td>Blue</td>
        <td>Most fire types (multi-purpose)</td>
    </tr>
</table>

<h3>The PASS Technique</h3>
<p>Remember <strong>PASS</strong> when using a fire extinguisher:</p>
<ol>
    <li><strong>P</strong>ull the pin</li>
    <li><strong>A</strong>im at the base of the fire</li>
    <li><strong>S</strong>queeze the handle</li>
    <li><strong>S</strong>weep from side to side</li>
</ol>

<h3>Important Safety Points</h3>
<ul>
    <li>Only tackle a fire if it is safe to do so and you have a clear escape route</li>
    <li>Always keep the fire between you and the exit</li>
    <li>Never use water on electrical fires or burning oil/fat</li>
    <li>If in doubt, leave the building and wait for the fire service</li>
    <li>Report all extinguisher usage, even if the fire is out</li>
</ul>

<div class='tip'>
    <strong>💡 Remember:</strong> Fire extinguishers are designed for small fires only. If the fire is larger than a waste bin, evacuate immediately and call the fire service.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 3,
                Title = "Alarm Response",
                Content = @"<h2>Fire Alarm Response</h2>
<p>When the fire alarm sounds, your immediate response can save lives. All personnel must be familiar with the correct alarm response procedures.</p>

<h3>When You Hear the Alarm</h3>
<ol>
    <li><strong>Stop work immediately</strong> - Make safe any equipment you're using if it can be done in seconds</li>
    <li><strong>Do not investigate</strong> - Treat every alarm as real</li>
    <li><strong>Leave by the nearest safe exit</strong> - Do not use lifts</li>
    <li><strong>Walk, don't run</strong> - Stay calm and help others</li>
    <li><strong>Close doors behind you</strong> - This slows fire spread</li>
    <li><strong>Go directly to the assembly point</strong> - Do not stop to collect belongings</li>
</ol>

<h3>Types of Alarm</h3>
<ul>
    <li><strong>Continuous alarm:</strong> Evacuate immediately</li>
    <li><strong>Intermittent alarm:</strong> Prepare to evacuate, await further instruction</li>
    <li><strong>Voice announcement:</strong> Follow specific instructions given</li>
</ul>

<h3>If You Discover a Fire</h3>
<ol>
    <li>Raise the alarm immediately - break glass call point or shout ""FIRE!""</li>
    <li>Call 999/112 or have someone else call</li>
    <li>Only attempt to fight the fire if trained, it's small, and you have a clear escape</li>
    <li>Close doors to contain the fire if possible</li>
    <li>Evacuate using the nearest safe route</li>
</ol>

<div class='warning'>
    <strong>⚠️ Never ignore an alarm:</strong> Even if you think it's a drill or false alarm, you must evacuate. Failing to respond to alarms puts yourself and others at risk.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 4,
                Title = "Evacuation Routes",
                Content = @"<h2>Evacuation Routes</h2>
<p>Knowing your evacuation routes before an emergency occurs is crucial. Construction sites change frequently, so it's important to stay updated on current escape routes.</p>

<h3>Key Principles</h3>
<ul>
    <li><strong>Know your routes:</strong> Identify at least two escape routes from your work area</li>
    <li><strong>Check daily:</strong> Routes may change as construction progresses</li>
    <li><strong>Keep routes clear:</strong> Never block escape routes with materials or equipment</li>
    <li><strong>Report obstructions:</strong> Inform your supervisor immediately if routes are blocked</li>
</ul>

<h3>Emergency Exit Signs</h3>
<p>Learn to recognize emergency signage:</p>
<ul>
    <li><strong>Green running man:</strong> Direction to emergency exit</li>
    <li><strong>Green door:</strong> Emergency exit door</li>
    <li><strong>Red fire equipment:</strong> Location of fire extinguishers, alarms, etc.</li>
</ul>

<h3>Site-Specific Routes</h3>
<p>For your current work location:</p>
<ul>
    <li>Locate the nearest emergency exit from your work area</li>
    <li>Identify an alternative route in case the primary route is blocked</li>
    <li>Know where the fire alarm call points are located</li>
    <li>Note the location of fire extinguishers along your route</li>
</ul>

<h3>Assisting Others</h3>
<ul>
    <li>Help visitors and new workers who may not know the routes</li>
    <li>Assist anyone with mobility difficulties</li>
    <li>If someone is unaccounted for, inform the fire marshal - never re-enter the building</li>
</ul>

<div class='tip'>
    <strong>💡 Daily Habit:</strong> Each day when you arrive on site, take 30 seconds to identify your escape routes from your work area.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 5,
                Title = "Assembly Points",
                Content = @"<h2>Assembly Points</h2>
<p>Assembly points are designated safe areas where everyone gathers after evacuation. This allows for accurate headcounts and ensures no one is left in danger.</p>

<h3>Assembly Point Requirements</h3>
<ul>
    <li>Located at a safe distance from the building/site</li>
    <li>Clearly marked and signed</li>
    <li>Large enough to accommodate all personnel</li>
    <li>Away from access routes for emergency services</li>
    <li>Upwind of the building if possible</li>
</ul>

<h3>Your Responsibilities at the Assembly Point</h3>
<ol>
    <li><strong>Go directly there</strong> - Don't stop for tea, equipment, or personal items</li>
    <li><strong>Report to your supervisor or fire marshal</strong> - They need to account for everyone</li>
    <li><strong>Stay at the assembly point</strong> - Don't leave until officially released</li>
    <li><strong>Report any missing persons</strong> - If you know someone was in the building</li>
    <li><strong>Keep access clear</strong> - Stay out of the way of emergency services</li>
</ol>

<h3>Roll Call Procedure</h3>
<ul>
    <li>Fire marshals will conduct a headcount</li>
    <li>Answer clearly when your name is called</li>
    <li>Report any visitors or contractors who were with you</li>
    <li>Do not leave until given the all-clear</li>
</ul>

<h3>Current Site Assembly Point</h3>
<p>Your site assembly point location is shown on the site safety board at the main entrance. If you're unsure of the location, ask your supervisor before starting work.</p>

<div class='warning'>
    <strong>⚠️ Critical Rule:</strong> Never re-enter a building during a fire emergency, even to rescue someone. Inform fire service personnel of anyone missing and let them conduct the rescue.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            }
        };
    }

    private static List<ToolboxTalkQuestion> CreateFireSafetyQuestions(Guid toolboxTalkId)
    {
        return new List<ToolboxTalkQuestion>
        {
            new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                QuestionNumber = 1,
                QuestionText = "What does PASS stand for when using a fire extinguisher?",
                QuestionType = QuestionType.MultipleChoice,
                Options = JsonSerializer.Serialize(new[]
                {
                    "Pull, Aim, Squeeze, Sweep",
                    "Push, Aim, Spray, Sweep",
                    "Pull, Alert, Squeeze, Spray",
                    "Point, Aim, Squeeze, Spray"
                }),
                CorrectAnswer = "Pull, Aim, Squeeze, Sweep",
                CorrectOptionIndex = 0,
                Points = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                QuestionNumber = 2,
                QuestionText = "Which type of fire extinguisher should NOT be used on electrical fires?",
                QuestionType = QuestionType.MultipleChoice,
                Options = JsonSerializer.Serialize(new[]
                {
                    "Water (Red band)",
                    "CO2 (Black band)",
                    "Dry Powder (Blue band)",
                    "Foam (Cream band)"
                }),
                CorrectAnswer = "Water (Red band)",
                CorrectOptionIndex = 0,
                Points = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                QuestionNumber = 3,
                QuestionText = "You should use the lift during a fire evacuation if it's faster.",
                QuestionType = QuestionType.TrueFalse,
                Options = null,
                CorrectAnswer = "False",
                Points = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                QuestionNumber = 4,
                QuestionText = "How long should a fire watch be maintained after completing hot work?",
                QuestionType = QuestionType.MultipleChoice,
                Options = JsonSerializer.Serialize(new[]
                {
                    "At least 30 minutes",
                    "5 minutes",
                    "Until the end of the shift",
                    "No fire watch is needed"
                }),
                CorrectAnswer = "At least 30 minutes",
                CorrectOptionIndex = 0,
                Points = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                QuestionNumber = 5,
                QuestionText = "What is the first thing you should do when you hear the fire alarm?",
                QuestionType = QuestionType.MultipleChoice,
                Options = JsonSerializer.Serialize(new[]
                {
                    "Stop work and evacuate by the nearest safe exit",
                    "Investigate where the fire is",
                    "Collect your personal belongings",
                    "Continue working until told to evacuate"
                }),
                CorrectAnswer = "Stop work and evacuate by the nearest safe exit",
                CorrectOptionIndex = 0,
                Points = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            }
        };
    }

    private static List<ToolboxTalkSection> CreateManualHandlingSections(Guid toolboxTalkId)
    {
        return new List<ToolboxTalkSection>
        {
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 1,
                Title = "Understanding Injury Risks",
                Content = @"<h2>Understanding Manual Handling Injury Risks</h2>
<p>Manual handling injuries are among the most common workplace injuries in construction. Understanding the risks is the first step to preventing injury.</p>

<h3>Common Manual Handling Injuries</h3>
<ul>
    <li><strong>Back injuries:</strong> Strains, sprains, and disc problems from lifting</li>
    <li><strong>Muscle strains:</strong> To shoulders, arms, and legs from overexertion</li>
    <li><strong>Repetitive strain injuries:</strong> From repeated lifting or carrying motions</li>
    <li><strong>Crush injuries:</strong> From dropped loads</li>
    <li><strong>Cuts and abrasions:</strong> From handling materials with sharp edges</li>
</ul>

<h3>Risk Factors (TILE)</h3>
<p>Assess manual handling risks using <strong>TILE</strong>:</p>
<ul>
    <li><strong>T - Task:</strong> Does it involve twisting, stooping, reaching, or repetitive movements?</li>
    <li><strong>I - Individual:</strong> Does the person have the physical capability? Any existing injuries?</li>
    <li><strong>L - Load:</strong> Is it heavy, bulky, difficult to grip, or unstable?</li>
    <li><strong>E - Environment:</strong> Are there space constraints, uneven floors, slopes, or obstacles?</li>
</ul>

<h3>When to Avoid Manual Handling</h3>
<p>Consider these questions before lifting:</p>
<ul>
    <li>Can the lift be avoided entirely by using mechanical aids?</li>
    <li>Is the load too heavy or awkward for one person?</li>
    <li>Are you fit and able to perform the lift safely?</li>
    <li>Is there a safer way to move this load?</li>
</ul>

<div class='warning'>
    <strong>⚠️ Key Principle:</strong> Avoid manual handling wherever possible. If a load can be moved by mechanical means, it should be.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 2,
                Title = "Safe Lifting Techniques",
                Content = @"<h2>Safe Lifting Techniques</h2>
<p>When manual handling cannot be avoided, using correct lifting techniques significantly reduces injury risk. The video accompanying this talk demonstrates these techniques.</p>

<h3>Before You Lift</h3>
<ol>
    <li><strong>Plan the lift:</strong> Where is the load going? Is the path clear?</li>
    <li><strong>Assess the load:</strong> Test the weight by rocking it slightly</li>
    <li><strong>Check for handles:</strong> Or identify the best grip points</li>
    <li><strong>Get help if needed:</strong> If the load is too heavy or awkward for one person</li>
</ol>

<h3>The Correct Lifting Technique</h3>
<ol>
    <li><strong>Position your feet:</strong> Shoulder-width apart, one foot slightly forward</li>
    <li><strong>Bend your knees:</strong> Not your back - squat down to the load</li>
    <li><strong>Get a good grip:</strong> Use your whole hand, not just fingers</li>
    <li><strong>Keep the load close:</strong> The closer to your body, the less strain</li>
    <li><strong>Lift with your legs:</strong> Straighten your legs to lift, keeping your back straight</li>
    <li><strong>Don't twist:</strong> Turn your whole body using your feet</li>
    <li><strong>Lower carefully:</strong> Bend your knees, keep control</li>
</ol>

<h3>Weight Guidelines</h3>
<p>General guideline weights for infrequent lifting:</p>
<ul>
    <li>Close to body, at waist height: Up to 25kg</li>
    <li>At arm's length: Up to 5kg</li>
    <li>At shoulder height: Up to 10kg</li>
    <li>Team lifts: Maximum 2/3 of combined individual capacities</li>
</ul>

<div class='tip'>
    <strong>💡 Remember:</strong> These are guidelines only. Individual capability, load characteristics, and environmental factors all affect what's safe.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 3,
                Title = "Using Handling Equipment",
                Content = @"<h2>Using Handling Equipment</h2>
<p>Mechanical handling equipment should be used wherever possible to eliminate or reduce manual handling risks. Knowing what equipment is available and how to use it safely is essential.</p>

<h3>Common Handling Equipment</h3>
<ul>
    <li><strong>Pallet trucks:</strong> For moving palletized loads</li>
    <li><strong>Sack trucks/hand trucks:</strong> For moving heavy items like gas cylinders</li>
    <li><strong>Trolleys:</strong> For moving materials around site</li>
    <li><strong>Lifting clamps:</strong> For handling sheet materials and panels</li>
    <li><strong>Vacuum lifters:</strong> For glass and smooth surfaces</li>
    <li><strong>Hoists and cranes:</strong> For heavy lifts (requires specific training)</li>
</ul>

<h3>Equipment Safety Rules</h3>
<ul>
    <li>Only use equipment you've been trained to use</li>
    <li>Check equipment before use - report any defects</li>
    <li>Don't exceed the safe working load (SWL)</li>
    <li>Ensure the load is secure before moving</li>
    <li>Maintain clear visibility - get a spotter if needed</li>
    <li>Keep pedestrians clear of moving equipment</li>
</ul>

<h3>Personal Protective Equipment</h3>
<p>When manually handling loads:</p>
<ul>
    <li><strong>Safety boots:</strong> Protect feet from dropped loads</li>
    <li><strong>Gloves:</strong> Improve grip and protect hands from sharp edges</li>
    <li><strong>High-visibility clothing:</strong> Ensure you're visible to equipment operators</li>
</ul>

<h3>Team Lifting</h3>
<p>When a load requires two or more people:</p>
<ul>
    <li>Appoint a leader to coordinate the lift</li>
    <li>All team members should be of similar height and capability</li>
    <li>Use clear commands: ""Ready? Lift!"" and ""Ready? Lower!""</li>
    <li>Move in unison - call out any issues immediately</li>
</ul>

<div class='warning'>
    <strong>⚠️ Equipment Training:</strong> Never operate lifting equipment without proper training. Using equipment incorrectly can cause serious injuries.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            }
        };
    }

    private static List<ToolboxTalkQuestion> CreateManualHandlingQuestions(Guid toolboxTalkId)
    {
        return new List<ToolboxTalkQuestion>
        {
            new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                QuestionNumber = 1,
                QuestionText = "What does TILE stand for in manual handling risk assessment?",
                QuestionType = QuestionType.MultipleChoice,
                Options = JsonSerializer.Serialize(new[]
                {
                    "Task, Individual, Load, Environment",
                    "Time, Intensity, Lift, Effort",
                    "Training, Instruction, Lifting, Equipment",
                    "Tool, Individual, Location, Equipment"
                }),
                CorrectAnswer = "Task, Individual, Load, Environment",
                CorrectOptionIndex = 0,
                Points = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                QuestionNumber = 2,
                QuestionText = "When lifting, you should bend at the waist to reach the load.",
                QuestionType = QuestionType.TrueFalse,
                Options = null,
                CorrectAnswer = "False",
                Points = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                QuestionNumber = 3,
                QuestionText = "What should you do before attempting to lift a load?",
                QuestionType = QuestionType.MultipleChoice,
                Options = JsonSerializer.Serialize(new[]
                {
                    "Plan the lift and assess the load",
                    "Just pick it up quickly",
                    "Only check if anyone is watching",
                    "Lift from one side only"
                }),
                CorrectAnswer = "Plan the lift and assess the load",
                CorrectOptionIndex = 0,
                Points = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                QuestionNumber = 4,
                QuestionText = "The best position for your feet when lifting is:",
                QuestionType = QuestionType.MultipleChoice,
                Options = JsonSerializer.Serialize(new[]
                {
                    "Shoulder-width apart, one foot slightly forward",
                    "Close together with knees locked",
                    "As far apart as possible",
                    "Standing on one foot"
                }),
                CorrectAnswer = "Shoulder-width apart, one foot slightly forward",
                CorrectOptionIndex = 0,
                Points = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            }
        };
    }

    private static List<ToolboxTalkSection> CreatePpeSections(Guid toolboxTalkId)
    {
        return new List<ToolboxTalkSection>
        {
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 1,
                Title = "Hard Hats",
                Content = @"<h2>Hard Hats - Head Protection</h2>
<p>Hard hats are mandatory on all QUANTUMBUILD construction sites. Head protection is one of the most critical pieces of PPE, protecting against falling objects, impacts, and electrical hazards.</p>

<h3>When to Wear a Hard Hat</h3>
<ul>
    <li>At all times when on the construction site (unless in designated safe areas)</li>
    <li>When working at height</li>
    <li>When working below others</li>
    <li>Near crane or lifting operations</li>
    <li>In any area where there's a risk of head injury</li>
</ul>

<h3>Hard Hat Standards</h3>
<p>All hard hats must meet EN 397 standard and be:</p>
<ul>
    <li>In good condition with no cracks, dents, or damage</li>
    <li>Within their use-by date (check inside shell)</li>
    <li>Properly adjusted to fit securely</li>
    <li>The correct type for the hazards present</li>
</ul>

<h3>Hard Hat Care</h3>
<ul>
    <li>Inspect before each use for damage</li>
    <li>Replace immediately if dropped from height or struck</li>
    <li>Clean regularly with mild soap and water</li>
    <li>Don't store in direct sunlight or extreme temperatures</li>
    <li>Never modify, drill holes, or apply stickers that could affect integrity</li>
    <li>Replace the harness if worn or damaged</li>
</ul>

<h3>Color Coding</h3>
<p>QUANTUMBUILD sites use the following hard hat colors:</p>
<ul>
    <li><strong>White:</strong> Site managers and supervisors</li>
    <li><strong>Blue:</strong> General workers</li>
    <li><strong>Orange:</strong> Visitors</li>
    <li><strong>Green:</strong> Safety officers</li>
    <li><strong>Yellow:</strong> General use</li>
</ul>

<div class='warning'>
    <strong>⚠️ Replacement:</strong> Hard hats should be replaced every 5 years from manufacture date, or immediately after any significant impact.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 2,
                Title = "High-Visibility Clothing",
                Content = @"<h2>High-Visibility Clothing</h2>
<p>High-visibility (hi-vis) clothing is essential for ensuring workers are visible to vehicle operators, crane drivers, and others on site. It is mandatory PPE on all QUANTUMBUILD sites.</p>

<h3>Hi-Vis Requirements</h3>
<ul>
    <li>Must meet EN ISO 20471 standard</li>
    <li>Class 2 or Class 3 hi-vis required depending on the area</li>
    <li>Must be worn at all times in operational areas</li>
    <li>Both jacket/vest AND trousers may be required in high-risk zones</li>
</ul>

<h3>Hi-Vis Classes</h3>
<table>
    <tr>
        <th>Class</th>
        <th>Coverage</th>
        <th>When Used</th>
    </tr>
    <tr>
        <td>Class 1</td>
        <td>Minimal</td>
        <td>Low risk areas only (rarely suitable for construction)</td>
    </tr>
    <tr>
        <td>Class 2</td>
        <td>Medium - vest or waistcoat</td>
        <td>General construction activities</td>
    </tr>
    <tr>
        <td>Class 3</td>
        <td>High - full body coverage</td>
        <td>Working near traffic, night work, high-risk areas</td>
    </tr>
</table>

<h3>Hi-Vis Care and Maintenance</h3>
<ul>
    <li>Wash according to manufacturer instructions</li>
    <li>Avoid bleach or fabric softeners which degrade the material</li>
    <li>Replace when fluorescent material is faded or reflective strips are damaged</li>
    <li>Keep clean - dirty hi-vis loses its effectiveness</li>
    <li>Don't modify by cutting or removing strips</li>
</ul>

<h3>Special Considerations</h3>
<ul>
    <li><strong>Night work:</strong> Class 3 hi-vis with reflective strips is mandatory</li>
    <li><strong>Near traffic:</strong> Long sleeves and full-length trousers required</li>
    <li><strong>Winter:</strong> Hi-vis must be worn as outermost layer</li>
</ul>

<div class='tip'>
    <strong>💡 Tip:</strong> Even in hot weather, hi-vis must be worn. Choose lightweight, breathable hi-vis garments for summer work.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 3,
                Title = "Safety Footwear",
                Content = @"<h2>Safety Footwear</h2>
<p>Safety boots or shoes are mandatory on construction sites. They protect against crushing, punctures, slips, and other foot injuries.</p>

<h3>Safety Boot Standards</h3>
<p>All safety footwear must meet EN ISO 20345 standard with appropriate ratings:</p>
<ul>
    <li><strong>S1:</strong> Basic toe protection, closed heel, antistatic, energy absorbing heel</li>
    <li><strong>S2:</strong> S1 + water resistant upper</li>
    <li><strong>S3:</strong> S2 + puncture resistant sole (Required for most construction work)</li>
    <li><strong>S5:</strong> Wellington boot with S3 features</li>
</ul>

<h3>Required Features for Construction</h3>
<ul>
    <li><strong>Steel or composite toe cap:</strong> 200 joule impact protection</li>
    <li><strong>Midsole protection:</strong> Puncture resistant to 1100N</li>
    <li><strong>Slip-resistant sole:</strong> SRC rated for oil and water</li>
    <li><strong>Ankle support:</strong> Full boot style for most work</li>
</ul>

<h3>Footwear Care</h3>
<ul>
    <li>Inspect daily for damage, wear, or deterioration</li>
    <li>Keep clean and dry - remove debris from soles</li>
    <li>Replace when worn, damaged, or compromised</li>
    <li>Ensure proper fit - too loose or too tight is dangerous</li>
    <li>Replace laces when worn</li>
</ul>

<h3>When to Replace</h3>
<p>Replace safety footwear when:</p>
<ul>
    <li>Toe cap is exposed or damaged</li>
    <li>Sole is worn smooth or separating</li>
    <li>Upper is cracked or worn through</li>
    <li>Waterproofing has failed</li>
    <li>Boots no longer provide adequate support</li>
</ul>

<div class='warning'>
    <strong>⚠️ Important:</strong> Trainers, sandals, or regular shoes are NEVER acceptable on construction sites, even for short visits.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 4,
                Title = "Hand Protection",
                Content = @"<h2>Hand Protection</h2>
<p>Hand injuries are among the most common construction site injuries. Using the correct gloves for each task significantly reduces the risk of cuts, abrasions, chemical burns, and other hand injuries.</p>

<h3>Types of Safety Gloves</h3>
<table>
    <tr>
        <th>Type</th>
        <th>Protection</th>
        <th>Common Uses</th>
    </tr>
    <tr>
        <td>General purpose</td>
        <td>Abrasion, minor cuts</td>
        <td>General handling, light work</td>
    </tr>
    <tr>
        <td>Cut-resistant</td>
        <td>Cuts, slashes</td>
        <td>Handling sharp materials, glass, metal</td>
    </tr>
    <tr>
        <td>Chemical-resistant</td>
        <td>Chemicals, solvents</td>
        <td>Working with hazardous substances</td>
    </tr>
    <tr>
        <td>Impact-resistant</td>
        <td>Crush injuries</td>
        <td>Heavy handling, working with power tools</td>
    </tr>
    <tr>
        <td>Rigger gloves</td>
        <td>Abrasion, some impact</td>
        <td>Heavy manual handling</td>
    </tr>
</table>

<h3>Selecting the Right Gloves</h3>
<ul>
    <li>Identify the hazards for your specific task</li>
    <li>Choose gloves rated for those hazards</li>
    <li>Ensure proper fit - too loose or too tight affects dexterity</li>
    <li>Consider the need for tactile sensitivity</li>
    <li>Check compatibility with other PPE</li>
</ul>

<h3>Glove Care and Inspection</h3>
<ul>
    <li>Inspect before each use for holes, tears, or wear</li>
    <li>Replace damaged gloves immediately</li>
    <li>Don't use gloves for tasks they're not designed for</li>
    <li>Clean or replace contaminated gloves</li>
    <li>Store properly when not in use</li>
</ul>

<h3>When NOT to Wear Gloves</h3>
<ul>
    <li>When operating rotating machinery (drill press, lathe) - risk of entanglement</li>
    <li>When required dexterity cannot be achieved safely</li>
</ul>

<div class='tip'>
    <strong>💡 Remember:</strong> The right glove for the job makes work safer AND easier. Don't compromise by using the wrong type.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            }
        };
    }

    private static List<ToolboxTalkSection> CreateElectricalSafetySections(Guid toolboxTalkId)
    {
        return new List<ToolboxTalkSection>
        {
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 1,
                Title = "Electrical Hazards on Site",
                Content = @"<h2>Electrical Hazards on Construction Sites</h2>
<p>Electricity is one of the most dangerous hazards on construction sites. Contact with electricity can cause electric shock, burns, and death. Understanding the hazards is essential for working safely.</p>

<h3>Sources of Electrical Hazard</h3>
<ul>
    <li><strong>Overhead power lines:</strong> Especially dangerous for crane and plant operators</li>
    <li><strong>Underground cables:</strong> Risk during excavation work</li>
    <li><strong>Temporary site electrics:</strong> Distribution boards, trailing cables</li>
    <li><strong>Power tools:</strong> Damaged cables, wet conditions</li>
    <li><strong>Fixed installations:</strong> During fit-out and commissioning phases</li>
</ul>

<h3>How Electrical Injuries Occur</h3>
<ul>
    <li><strong>Direct contact:</strong> Touching live conductors</li>
    <li><strong>Indirect contact:</strong> Touching equipment that has become live due to a fault</li>
    <li><strong>Arcing:</strong> Electricity jumping across a gap (very high voltage)</li>
    <li><strong>Burns:</strong> From electrical arcs or fires</li>
    <li><strong>Falls:</strong> Caused by the shock reaction</li>
</ul>

<h3>Effects of Electric Shock</h3>
<p>The severity depends on the voltage, current, path through body, and duration:</p>
<ul>
    <li>Mild tingling sensation (low current)</li>
    <li>Muscle spasms and inability to let go</li>
    <li>Burns - internal and external</li>
    <li>Cardiac arrest</li>
    <li>Death</li>
</ul>

<div class='warning'>
    <strong>⚠️ Critical:</strong> Even low voltages (110V) can be fatal. Never assume any electrical source is safe without proper verification.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 2,
                Title = "Safe Working Practices",
                Content = @"<h2>Safe Working Practices with Electricity</h2>
<p>Following safe working practices is essential when working near electrical hazards. Many electrical accidents are preventable with proper precautions.</p>

<h3>General Safety Rules</h3>
<ul>
    <li>Only qualified electricians should work on electrical installations</li>
    <li>Assume all electrical equipment is live until proven otherwise</li>
    <li>Never work on live electrical systems</li>
    <li>Use 110V reduced voltage tools where possible</li>
    <li>Keep electrical equipment away from water</li>
    <li>Report any damaged cables, plugs, or equipment immediately</li>
</ul>

<h3>Before Using Electrical Equipment</h3>
<ol>
    <li>Check the equipment is suitable for the task and environment</li>
    <li>Visually inspect cables, plugs, and casing for damage</li>
    <li>Ensure equipment has been PAT tested (check the label)</li>
    <li>Check RCD protection is in place and working</li>
    <li>Ensure trailing cables are positioned safely</li>
</ol>

<h3>Using Electrical Equipment Safely</h3>
<ul>
    <li>Don't use equipment in wet conditions unless specifically rated</li>
    <li>Don't overload sockets or use multiple adaptors</li>
    <li>Don't run cables through water or across walkways</li>
    <li>Disconnect equipment when not in use</li>
    <li>Pull the plug, not the cable, when disconnecting</li>
    <li>Store equipment properly when finished</li>
</ul>

<h3>Working Near Overhead Lines</h3>
<ul>
    <li>Maintain safe clearance distances (consult site rules)</li>
    <li>Use goal posts and barriers where needed</li>
    <li>Assume lines are live unless confirmed isolated</li>
    <li>Report any contact or near-miss immediately</li>
</ul>

<div class='tip'>
    <strong>💡 Remember:</strong> When in doubt, stop work and consult a qualified electrician. Electrical work is one area where taking risks is never acceptable.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 3,
                Title = "Isolation Procedures",
                Content = @"<h2>Electrical Isolation Procedures</h2>
<p>Proper isolation of electrical supplies is critical before any work on electrical systems. Isolation must only be performed by competent persons.</p>

<h3>Safe Isolation Procedure</h3>
<ol>
    <li><strong>Identify:</strong> Confirm the correct circuit to be isolated</li>
    <li><strong>Isolate:</strong> Switch off and isolate the supply at the appropriate point</li>
    <li><strong>Secure:</strong> Lock off using personal padlock, apply warning tags</li>
    <li><strong>Prove:</strong> Test the circuit is dead using approved voltage indicator</li>
    <li><strong>Verify tester:</strong> Prove the voltage indicator works before and after testing</li>
</ol>

<h3>Lock Out/Tag Out (LOTO)</h3>
<p>LOTO is a critical safety procedure:</p>
<ul>
    <li>Use your own personal padlock</li>
    <li>Keep the key on your person at all times</li>
    <li>Attach a danger tag with your name and date</li>
    <li>Never remove another person's lock without authorization</li>
    <li>Multiple workers = multiple locks on the same point</li>
</ul>

<h3>After Work is Complete</h3>
<ol>
    <li>Ensure all work is complete and tools removed</li>
    <li>Confirm all personnel are clear</li>
    <li>Remove your lock and tag</li>
    <li>Notify appropriate personnel before re-energizing</li>
    <li>Test operation after re-energizing</li>
</ol>

<h3>Emergency Isolation</h3>
<p>Know the location of:</p>
<ul>
    <li>Emergency stop buttons</li>
    <li>Main isolator switches</li>
    <li>Distribution board locations</li>
</ul>

<div class='warning'>
    <strong>⚠️ Competence Required:</strong> Only competent persons should perform electrical isolation. If you're not trained, seek assistance from a qualified electrician.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 4,
                Title = "Emergency Response",
                Content = @"<h2>Electrical Emergency Response</h2>
<p>Knowing how to respond to an electrical emergency can save lives. Quick, correct action is essential.</p>

<h3>If Someone is Being Electrocuted</h3>
<ol>
    <li><strong>Don't touch them</strong> - you could become a victim too</li>
    <li><strong>Switch off the power</strong> at the socket or distribution board if safe to do so</li>
    <li><strong>If you can't switch off:</strong> Use a dry, non-conductive object (wooden broom, dry rope) to separate the person from the source</li>
    <li><strong>Call emergency services</strong> - dial 999/112</li>
    <li><strong>Begin CPR</strong> if trained and the person is not breathing</li>
    <li><strong>Treat for shock</strong> - keep warm, raise legs if possible</li>
</ol>

<h3>Electrical Burns</h3>
<ul>
    <li>Electrical burns can be more serious than they appear</li>
    <li>Internal tissue damage may not be visible</li>
    <li>Cool burns with clean, cool water</li>
    <li>Cover with clean, non-fluffy dressing</li>
    <li>All electrical burn victims should receive medical attention</li>
</ul>

<h3>Reporting Requirements</h3>
<p>All electrical incidents must be reported:</p>
<ul>
    <li>Electric shocks (even minor)</li>
    <li>Near misses</li>
    <li>Damaged electrical equipment</li>
    <li>Arcing or sparking</li>
    <li>Burning smells from electrical equipment</li>
</ul>

<h3>Site-Specific Information</h3>
<p>Make sure you know:</p>
<ul>
    <li>Location of emergency isolation switches</li>
    <li>Location of first aid equipment</li>
    <li>Emergency contact numbers</li>
    <li>Location of AED (defibrillator) if available</li>
</ul>

<div class='warning'>
    <strong>⚠️ Your Safety First:</strong> Never put yourself at risk to help an electrocution victim. A dead rescuer cannot help anyone. Always isolate the power first if possible.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                SectionNumber = 5,
                Title = "Equipment Inspection",
                Content = @"<h2>Electrical Equipment Inspection</h2>
<p>Regular inspection of electrical equipment helps identify hazards before they cause injury. All workers should be able to perform basic visual inspections.</p>

<h3>Pre-Use Visual Inspection</h3>
<p>Before using any electrical equipment, check for:</p>
<ul>
    <li><strong>Cables:</strong> Cuts, abrasions, damage to outer sheath, exposed wires</li>
    <li><strong>Plugs:</strong> Cracks, burn marks, bent pins, cable grip secure</li>
    <li><strong>Sockets:</strong> Damage, burn marks, loose connections</li>
    <li><strong>Equipment body:</strong> Cracks, damage, missing guards or covers</li>
    <li><strong>On/off switch:</strong> Working correctly</li>
    <li><strong>PAT test label:</strong> Current and valid</li>
</ul>

<h3>PAT Testing</h3>
<p>Portable Appliance Testing (PAT) must be performed:</p>
<ul>
    <li>At regular intervals based on equipment type and use</li>
    <li>After any repair work</li>
    <li>If damage is suspected</li>
</ul>
<p>Check the PAT label shows:</p>
<ul>
    <li>Test date within valid period</li>
    <li>Tester identification</li>
    <li>Pass status</li>
</ul>

<h3>What to Do if Defects Are Found</h3>
<ol>
    <li><strong>Stop using the equipment immediately</strong></li>
    <li><strong>Disconnect from power</strong></li>
    <li><strong>Tag as defective</strong> (""Do Not Use"" label)</li>
    <li><strong>Report to supervisor</strong></li>
    <li><strong>Remove from service</strong> until repaired and tested</li>
</ol>

<h3>Extension Leads and Cables</h3>
<ul>
    <li>Uncoil fully before use to prevent overheating</li>
    <li>Don't join cables with tape - use proper connectors</li>
    <li>Route cables safely - use cable covers on walkways</li>
    <li>Don't run cables through doors or windows</li>
    <li>Use weatherproof equipment outdoors</li>
</ul>

<div class='tip'>
    <strong>💡 Daily Habit:</strong> Make equipment inspection part of your routine. A 30-second check before use could prevent a serious accident.
</div>",
                RequiresAcknowledgment = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            }
        };
    }

    private static List<ToolboxTalkQuestion> CreateElectricalSafetyQuestions(Guid toolboxTalkId)
    {
        return new List<ToolboxTalkQuestion>
        {
            new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                QuestionNumber = 1,
                QuestionText = "What voltage should portable tools use on construction sites where possible?",
                QuestionType = QuestionType.MultipleChoice,
                Options = JsonSerializer.Serialize(new[]
                {
                    "110V",
                    "230V",
                    "415V",
                    "12V"
                }),
                CorrectAnswer = "110V",
                CorrectOptionIndex = 0,
                Points = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                QuestionNumber = 2,
                QuestionText = "What is the first step in the safe isolation procedure?",
                QuestionType = QuestionType.MultipleChoice,
                Options = JsonSerializer.Serialize(new[]
                {
                    "Identify the correct circuit",
                    "Apply a padlock",
                    "Start working on the circuit",
                    "Test the circuit is dead"
                }),
                CorrectAnswer = "Identify the correct circuit",
                CorrectOptionIndex = 0,
                Points = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                QuestionNumber = 3,
                QuestionText = "You can touch someone being electrocuted to pull them away from the source.",
                QuestionType = QuestionType.TrueFalse,
                Options = null,
                CorrectAnswer = "False",
                Points = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                QuestionNumber = 4,
                QuestionText = "What should you check on electrical equipment before use?",
                QuestionType = QuestionType.MultipleChoice,
                Options = JsonSerializer.Serialize(new[]
                {
                    "Cables, plugs, and PAT test label",
                    "Only the on/off switch",
                    "Just if it powers on",
                    "Nothing, if it worked yesterday"
                }),
                CorrectAnswer = "Cables, plugs, and PAT test label",
                CorrectOptionIndex = 0,
                Points = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalkId,
                QuestionNumber = 5,
                QuestionText = "What should you do if you find a defective piece of electrical equipment?",
                QuestionType = QuestionType.MultipleChoice,
                Options = JsonSerializer.Serialize(new[]
                {
                    "Stop using it, tag as defective, and report to supervisor",
                    "Continue using it carefully",
                    "Try to repair it yourself",
                    "Just put it back in the store"
                }),
                CorrectAnswer = "Stop using it, tag as defective, and report to supervisor",
                CorrectOptionIndex = 0,
                Points = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            }
        };
    }

    #endregion
}
