using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalk;

public class UpdateToolboxTalkCommandHandler : IRequestHandler<UpdateToolboxTalkCommand, ToolboxTalkDto>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<UpdateToolboxTalkCommandHandler> _logger;

    public UpdateToolboxTalkCommandHandler(
        IToolboxTalksDbContext dbContext,
        ICurrentUserService currentUser,
        ILogger<UpdateToolboxTalkCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<ToolboxTalkDto> Handle(UpdateToolboxTalkCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[DEBUG] UpdateToolboxTalk received. Id: {Id}, TenantId: {TenantId}, " +
            "SectionCount: {SectionCount}, SectionsWithIds: {WithIds}, SectionsWithoutIds: {WithoutIds}, " +
            "QuestionCount: {QuestionCount}, QuestionsWithIds: {QWithIds}, QuestionsWithoutIds: {QWithoutIds}",
            request.Id, request.TenantId,
            request.Sections?.Count ?? 0,
            request.Sections?.Count(s => s.Id.HasValue) ?? 0,
            request.Sections?.Count(s => !s.Id.HasValue) ?? 0,
            request.Questions?.Count ?? 0,
            request.Questions?.Count(q => q.Id.HasValue) ?? 0,
            request.Questions?.Count(q => !q.Id.HasValue) ?? 0);

        // Log each section's ID and source
        foreach (var section in request.Sections ?? Enumerable.Empty<UpdateToolboxTalkSectionDto>())
        {
            _logger.LogInformation(
                "[DEBUG] Section received: Id={Id}, SectionNumber={Num}, Source={Source}, Title={Title}",
                section.Id?.ToString() ?? "NULL", section.SectionNumber, section.Source,
                section.Title?.Length > 40 ? section.Title.Substring(0, 40) + "..." : section.Title);
        }

        // Log each question's ID and source
        foreach (var question in request.Questions ?? Enumerable.Empty<UpdateToolboxTalkQuestionDto>())
        {
            _logger.LogInformation(
                "[DEBUG] Question received: Id={Id}, QuestionNumber={Num}, Source={Source}, Type={Type}",
                question.Id?.ToString() ?? "NULL", question.QuestionNumber, question.Source, question.QuestionType);
        }

        // Find the toolbox talk (without sections and questions - we'll query those separately)
        var toolboxTalk = await _dbContext.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (toolboxTalk == null)
        {
            throw new KeyNotFoundException($"Learning with ID {request.Id} not found.");
        }

        // Validate tenant ownership
        if (toolboxTalk.TenantId != request.TenantId)
        {
            throw new UnauthorizedAccessException("Access denied to this learning.");
        }

        // Validate title uniqueness (excluding current talk)
        if (toolboxTalk.Title != request.Title)
        {
            var titleExists = await _dbContext.ToolboxTalks
                .AnyAsync(t => t.TenantId == request.TenantId &&
                              t.Title == request.Title &&
                              t.Id != request.Id, cancellationToken);

            if (titleExists)
            {
                throw new InvalidOperationException($"A learning with title '{request.Title}' already exists.");
            }
        }

        // Update basic properties
        toolboxTalk.Title = request.Title;
        toolboxTalk.Description = request.Description;
        toolboxTalk.Category = request.Category;
        toolboxTalk.Frequency = request.Frequency;
        toolboxTalk.VideoUrl = request.VideoUrl;
        toolboxTalk.VideoSource = request.VideoSource;
        toolboxTalk.AttachmentUrl = request.AttachmentUrl;
        toolboxTalk.MinimumVideoWatchPercent = request.MinimumVideoWatchPercent;
        toolboxTalk.RequiresQuiz = request.RequiresQuiz;
        toolboxTalk.PassingScore = request.RequiresQuiz ? request.PassingScore : null;
        toolboxTalk.IsActive = request.IsActive;
        toolboxTalk.QuizQuestionCount = request.RequiresQuiz ? request.QuizQuestionCount : null;
        toolboxTalk.ShuffleQuestions = request.RequiresQuiz && request.ShuffleQuestions;
        toolboxTalk.ShuffleOptions = request.RequiresQuiz && request.ShuffleOptions;
        toolboxTalk.UseQuestionPool = request.RequiresQuiz && request.UseQuestionPool;
        toolboxTalk.AutoAssignToNewEmployees = request.AutoAssignToNewEmployees;
        toolboxTalk.AutoAssignDueDays = request.AutoAssignDueDays;
        toolboxTalk.SourceLanguageCode = request.SourceLanguageCode;
        toolboxTalk.GenerateSlidesFromPdf = request.GenerateSlidesFromPdf;
        toolboxTalk.GenerateCertificate = request.GenerateCertificate;
        toolboxTalk.RequiresRefresher = request.RequiresRefresher;
        toolboxTalk.RefresherIntervalMonths = request.RefresherIntervalMonths;
        toolboxTalk.UpdatedAt = DateTime.UtcNow;
        toolboxTalk.UpdatedBy = _currentUser.UserId;

        // Handle sections update - query from DbContext directly to avoid concurrency issues
        await UpdateSectionsAsync(toolboxTalk, request.Sections, cancellationToken);

        // Handle questions update - query from DbContext directly to avoid concurrency issues
        await UpdateQuestionsAsync(toolboxTalk, request.Questions, cancellationToken);

        _logger.LogInformation("[DEBUG] Calling SaveChangesAsync for UpdateToolboxTalk {Id}...", toolboxTalk.Id);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("[DEBUG] SaveChangesAsync completed for UpdateToolboxTalk {Id}", toolboxTalk.Id);

        // Reload sections and questions for the response
        var sections = await _dbContext.ToolboxTalkSections
            .Where(s => s.ToolboxTalkId == toolboxTalk.Id && !s.IsDeleted)
            .OrderBy(s => s.SectionNumber)
            .ToListAsync(cancellationToken);

        var questions = await _dbContext.ToolboxTalkQuestions
            .Where(q => q.ToolboxTalkId == toolboxTalk.Id && !q.IsDeleted)
            .OrderBy(q => q.QuestionNumber)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "[DEBUG] After save - Sections in DB (non-deleted): {SectionCount}, Questions in DB (non-deleted): {QuestionCount}",
            sections.Count, questions.Count);

        foreach (var s in sections)
        {
            _logger.LogInformation(
                "[DEBUG] Final section: Id={Id}, Num={Num}, Source={Source}, Title={Title}",
                s.Id, s.SectionNumber, s.Source, s.Title?.Length > 40 ? s.Title.Substring(0, 40) + "..." : s.Title);
        }

        return MapToDto(toolboxTalk, sections, questions);
    }

    private async Task UpdateSectionsAsync(ToolboxTalk toolboxTalk, List<UpdateToolboxTalkSectionDto> sectionDtos, CancellationToken cancellationToken)
    {
        // Get existing non-deleted sections directly from DbContext
        var existingSections = await _dbContext.ToolboxTalkSections
            .Where(s => s.ToolboxTalkId == toolboxTalk.Id && !s.IsDeleted)
            .ToListAsync(cancellationToken);

        var existingSectionIds = existingSections.Select(s => s.Id).ToHashSet();
        var incomingSectionIds = sectionDtos
            .Where(s => s.Id.HasValue)
            .Select(s => s.Id!.Value)
            .ToHashSet();

        _logger.LogInformation(
            "[DEBUG] UpdateSectionsAsync: ExistingSections={ExistingCount} (IDs: {ExistingIds}), " +
            "IncomingSections={IncomingCount} (IDs with value: {IncomingIds}), " +
            "Sections to soft-delete: {DeleteCount}, New sections (no ID): {NewCount}",
            existingSections.Count,
            string.Join(", ", existingSectionIds),
            sectionDtos.Count,
            string.Join(", ", incomingSectionIds),
            existingSectionIds.Except(incomingSectionIds).Count(),
            sectionDtos.Count(s => !s.Id.HasValue));

        // Soft-delete sections that are in DB but not in request
        foreach (var section in existingSections)
        {
            if (!incomingSectionIds.Contains(section.Id))
            {
                section.IsDeleted = true;
                section.UpdatedAt = DateTime.UtcNow;
                section.UpdatedBy = _currentUser.UserId;
            }
        }

        // Process incoming sections
        foreach (var sectionDto in sectionDtos)
        {
            if (sectionDto.Id.HasValue)
            {
                // Try to find existing section
                var existingSection = existingSections.FirstOrDefault(s => s.Id == sectionDto.Id.Value);

                if (existingSection != null)
                {
                    // Update existing section
                    existingSection.SectionNumber = sectionDto.SectionNumber;
                    existingSection.Title = sectionDto.Title;
                    existingSection.Content = sectionDto.Content;
                    existingSection.RequiresAcknowledgment = sectionDto.RequiresAcknowledgment;
                    existingSection.Source = sectionDto.Source;
                    existingSection.VideoTimestamp = sectionDto.VideoTimestamp;
                    existingSection.IsDeleted = false; // In case it was marked for deletion
                    existingSection.UpdatedAt = DateTime.UtcNow;
                    existingSection.UpdatedBy = _currentUser.UserId;
                }
                else
                {
                    // ID was provided but doesn't exist in DB - create as new
                    _logger.LogWarning("Section ID {SectionId} not found, creating as new", sectionDto.Id.Value);
                    var newSection = new ToolboxTalkSection
                    {
                        Id = Guid.NewGuid(),
                        ToolboxTalkId = toolboxTalk.Id,
                        SectionNumber = sectionDto.SectionNumber,
                        Title = sectionDto.Title,
                        Content = sectionDto.Content,
                        RequiresAcknowledgment = sectionDto.RequiresAcknowledgment,
                        Source = sectionDto.Source,
                        VideoTimestamp = sectionDto.VideoTimestamp,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = _currentUser.UserId
                    };
                    _dbContext.ToolboxTalkSections.Add(newSection);
                }
            }
            else
            {
                // No ID - create new section
                var newSection = new ToolboxTalkSection
                {
                    Id = Guid.NewGuid(),
                    ToolboxTalkId = toolboxTalk.Id,
                    SectionNumber = sectionDto.SectionNumber,
                    Title = sectionDto.Title,
                    Content = sectionDto.Content,
                    RequiresAcknowledgment = sectionDto.RequiresAcknowledgment,
                    Source = sectionDto.Source,
                    VideoTimestamp = sectionDto.VideoTimestamp,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = _currentUser.UserId
                };
                _dbContext.ToolboxTalkSections.Add(newSection);
            }
        }
    }

    private async Task UpdateQuestionsAsync(ToolboxTalk toolboxTalk, List<UpdateToolboxTalkQuestionDto> questionDtos, CancellationToken cancellationToken)
    {
        // Get existing non-deleted questions directly from DbContext
        var existingQuestions = await _dbContext.ToolboxTalkQuestions
            .Where(q => q.ToolboxTalkId == toolboxTalk.Id && !q.IsDeleted)
            .ToListAsync(cancellationToken);

        var existingQuestionIds = existingQuestions.Select(q => q.Id).ToHashSet();
        var incomingQuestionIds = questionDtos
            .Where(q => q.Id.HasValue)
            .Select(q => q.Id!.Value)
            .ToHashSet();

        // Soft-delete questions that are in DB but not in request
        foreach (var question in existingQuestions)
        {
            if (!incomingQuestionIds.Contains(question.Id))
            {
                question.IsDeleted = true;
                question.UpdatedAt = DateTime.UtcNow;
                question.UpdatedBy = _currentUser.UserId;
            }
        }

        // Process incoming questions
        foreach (var questionDto in questionDtos)
        {
            if (questionDto.Id.HasValue)
            {
                // Try to find existing question
                var existingQuestion = existingQuestions.FirstOrDefault(q => q.Id == questionDto.Id.Value);

                if (existingQuestion != null)
                {
                    // Update existing question
                    existingQuestion.QuestionNumber = questionDto.QuestionNumber;
                    existingQuestion.QuestionText = questionDto.QuestionText;
                    existingQuestion.QuestionType = questionDto.QuestionType;
                    existingQuestion.Options = questionDto.Options != null ? JsonSerializer.Serialize(questionDto.Options) : null;
                    existingQuestion.CorrectAnswer = questionDto.CorrectAnswer;
                    existingQuestion.CorrectOptionIndex = ToolboxTalkQuestion.ComputeCorrectOptionIndex(questionDto.CorrectAnswer, questionDto.Options);
                    existingQuestion.Points = questionDto.Points;
                    existingQuestion.Source = questionDto.Source;
                    existingQuestion.VideoTimestamp = questionDto.VideoTimestamp;
                    existingQuestion.IsFromVideoFinalPortion = questionDto.IsFromVideoFinalPortion;
                    existingQuestion.IsDeleted = false; // In case it was marked for deletion
                    existingQuestion.UpdatedAt = DateTime.UtcNow;
                    existingQuestion.UpdatedBy = _currentUser.UserId;
                }
                else
                {
                    // ID was provided but doesn't exist in DB - create as new
                    _logger.LogWarning("Question ID {QuestionId} not found, creating as new", questionDto.Id.Value);
                    var newQuestion = new ToolboxTalkQuestion
                    {
                        Id = Guid.NewGuid(),
                        ToolboxTalkId = toolboxTalk.Id,
                        QuestionNumber = questionDto.QuestionNumber,
                        QuestionText = questionDto.QuestionText,
                        QuestionType = questionDto.QuestionType,
                        Options = questionDto.Options != null ? JsonSerializer.Serialize(questionDto.Options) : null,
                        CorrectAnswer = questionDto.CorrectAnswer,
                        CorrectOptionIndex = ToolboxTalkQuestion.ComputeCorrectOptionIndex(questionDto.CorrectAnswer, questionDto.Options),
                        Points = questionDto.Points,
                        Source = questionDto.Source,
                        VideoTimestamp = questionDto.VideoTimestamp,
                        IsFromVideoFinalPortion = questionDto.IsFromVideoFinalPortion,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = _currentUser.UserId
                    };
                    _dbContext.ToolboxTalkQuestions.Add(newQuestion);
                }
            }
            else
            {
                // No ID - create new question
                var newQuestion = new ToolboxTalkQuestion
                {
                    Id = Guid.NewGuid(),
                    ToolboxTalkId = toolboxTalk.Id,
                    QuestionNumber = questionDto.QuestionNumber,
                    QuestionText = questionDto.QuestionText,
                    QuestionType = questionDto.QuestionType,
                    Options = questionDto.Options != null ? JsonSerializer.Serialize(questionDto.Options) : null,
                    CorrectAnswer = questionDto.CorrectAnswer,
                    CorrectOptionIndex = ToolboxTalkQuestion.ComputeCorrectOptionIndex(questionDto.CorrectAnswer, questionDto.Options),
                    Points = questionDto.Points,
                    Source = questionDto.Source,
                    VideoTimestamp = questionDto.VideoTimestamp,
                    IsFromVideoFinalPortion = questionDto.IsFromVideoFinalPortion,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = _currentUser.UserId
                };
                _dbContext.ToolboxTalkQuestions.Add(newQuestion);
            }
        }
    }

    private static ToolboxTalkDto MapToDto(ToolboxTalk entity, List<ToolboxTalkSection> sections, List<ToolboxTalkQuestion> questions)
    {
        return new ToolboxTalkDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Description = entity.Description,
            Category = entity.Category,
            Frequency = entity.Frequency,
            FrequencyDisplay = GetFrequencyDisplay(entity.Frequency),
            VideoUrl = entity.VideoUrl,
            VideoSource = entity.VideoSource,
            VideoSourceDisplay = GetVideoSourceDisplay(entity.VideoSource),
            AttachmentUrl = entity.AttachmentUrl,
            MinimumVideoWatchPercent = entity.MinimumVideoWatchPercent,
            RequiresQuiz = entity.RequiresQuiz,
            PassingScore = entity.PassingScore,
            IsActive = entity.IsActive,
            QuizQuestionCount = entity.QuizQuestionCount,
            ShuffleQuestions = entity.ShuffleQuestions,
            ShuffleOptions = entity.ShuffleOptions,
            UseQuestionPool = entity.UseQuestionPool,
            AutoAssignToNewEmployees = entity.AutoAssignToNewEmployees,
            AutoAssignDueDays = entity.AutoAssignDueDays,
            SourceLanguageCode = entity.SourceLanguageCode,
            GenerateCertificate = entity.GenerateCertificate,
            RequiresRefresher = entity.RequiresRefresher,
            RefresherIntervalMonths = entity.RefresherIntervalMonths,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Sections = sections
                .Select(s => new ToolboxTalkSectionDto
                {
                    Id = s.Id,
                    ToolboxTalkId = s.ToolboxTalkId,
                    SectionNumber = s.SectionNumber,
                    Title = s.Title,
                    Content = s.Content,
                    RequiresAcknowledgment = s.RequiresAcknowledgment,
                    Source = s.Source,
                    SourceDisplay = GetContentSourceDisplay(s.Source),
                    VideoTimestamp = s.VideoTimestamp
                }).ToList(),
            Questions = questions
                .Select(q => new ToolboxTalkQuestionDto
                {
                    Id = q.Id,
                    ToolboxTalkId = q.ToolboxTalkId,
                    QuestionNumber = q.QuestionNumber,
                    QuestionText = q.QuestionText,
                    QuestionType = q.QuestionType,
                    QuestionTypeDisplay = GetQuestionTypeDisplay(q.QuestionType),
                    Options = !string.IsNullOrEmpty(q.Options) ? JsonSerializer.Deserialize<List<string>>(q.Options) : null,
                    CorrectAnswer = q.CorrectAnswer,
                    Points = q.Points,
                    Source = q.Source,
                    SourceDisplay = GetContentSourceDisplay(q.Source),
                    VideoTimestamp = q.VideoTimestamp,
                    IsFromVideoFinalPortion = q.IsFromVideoFinalPortion
                }).ToList()
        };
    }

    private static string GetFrequencyDisplay(ToolboxTalkFrequency frequency) => frequency switch
    {
        ToolboxTalkFrequency.Once => "Once",
        ToolboxTalkFrequency.Weekly => "Weekly",
        ToolboxTalkFrequency.Monthly => "Monthly",
        ToolboxTalkFrequency.Annually => "Annually",
        _ => frequency.ToString()
    };

    private static string GetVideoSourceDisplay(VideoSource source) => source switch
    {
        VideoSource.None => "None",
        VideoSource.YouTube => "YouTube",
        VideoSource.GoogleDrive => "Google Drive",
        VideoSource.Vimeo => "Vimeo",
        VideoSource.DirectUrl => "Direct URL",
        _ => source.ToString()
    };

    private static string GetQuestionTypeDisplay(QuestionType type) => type switch
    {
        QuestionType.MultipleChoice => "Multiple Choice",
        QuestionType.TrueFalse => "True/False",
        QuestionType.ShortAnswer => "Short Answer",
        _ => type.ToString()
    };

    private static string GetContentSourceDisplay(ContentSource source) => source switch
    {
        ContentSource.Manual => "Manual",
        ContentSource.Video => "Video",
        ContentSource.Pdf => "PDF",
        ContentSource.Both => "Video & PDF",
        _ => source.ToString()
    };
}
