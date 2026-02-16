using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.CreateToolboxTalk;

public class CreateToolboxTalkCommandHandler : IRequestHandler<CreateToolboxTalkCommand, ToolboxTalkDto>
{
    private readonly IToolboxTalksDbContext _dbContext;

    public CreateToolboxTalkCommandHandler(IToolboxTalksDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ToolboxTalkDto> Handle(CreateToolboxTalkCommand request, CancellationToken cancellationToken)
    {
        // Validate title is unique within tenant
        var titleExists = await _dbContext.ToolboxTalks
            .AnyAsync(t => t.TenantId == request.TenantId && t.Title == request.Title, cancellationToken);

        if (titleExists)
        {
            throw new InvalidOperationException($"A toolbox talk with title '{request.Title}' already exists.");
        }

        // Create the toolbox talk
        var toolboxTalk = new ToolboxTalk
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Title = request.Title,
            Description = request.Description,
            Category = request.Category,
            Frequency = request.Frequency,
            VideoUrl = request.VideoUrl,
            VideoSource = request.VideoSource,
            AttachmentUrl = request.AttachmentUrl,
            MinimumVideoWatchPercent = request.MinimumVideoWatchPercent,
            RequiresQuiz = request.RequiresQuiz,
            PassingScore = request.RequiresQuiz ? request.PassingScore : null,
            IsActive = request.IsActive,
            QuizQuestionCount = request.RequiresQuiz ? request.QuizQuestionCount : null,
            ShuffleQuestions = request.RequiresQuiz && request.ShuffleQuestions,
            ShuffleOptions = request.RequiresQuiz && request.ShuffleOptions,
            UseQuestionPool = request.RequiresQuiz && request.UseQuestionPool,
            AutoAssignToNewEmployees = request.AutoAssignToNewEmployees,
            AutoAssignDueDays = request.AutoAssignDueDays,
            SourceLanguageCode = request.SourceLanguageCode,
            GenerateSlidesFromPdf = request.GenerateSlidesFromPdf
        };

        // Create sections
        foreach (var sectionDto in request.Sections)
        {
            var section = new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalk.Id,
                SectionNumber = sectionDto.SectionNumber,
                Title = sectionDto.Title,
                Content = sectionDto.Content,
                RequiresAcknowledgment = sectionDto.RequiresAcknowledgment
            };
            toolboxTalk.Sections.Add(section);
        }

        // Create questions
        foreach (var questionDto in request.Questions)
        {
            var question = new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalk.Id,
                QuestionNumber = questionDto.QuestionNumber,
                QuestionText = questionDto.QuestionText,
                QuestionType = questionDto.QuestionType,
                Options = questionDto.Options != null ? JsonSerializer.Serialize(questionDto.Options) : null,
                CorrectAnswer = questionDto.CorrectAnswer,
                CorrectOptionIndex = ToolboxTalkQuestion.ComputeCorrectOptionIndex(questionDto.CorrectAnswer, questionDto.Options),
                Points = questionDto.Points
            };
            toolboxTalk.Questions.Add(question);
        }

        _dbContext.ToolboxTalks.Add(toolboxTalk);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Return the created toolbox talk as DTO
        return MapToDto(toolboxTalk);
    }

    private static ToolboxTalkDto MapToDto(ToolboxTalk entity)
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
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Sections = entity.Sections.Select(s => new ToolboxTalkSectionDto
            {
                Id = s.Id,
                ToolboxTalkId = s.ToolboxTalkId,
                SectionNumber = s.SectionNumber,
                Title = s.Title,
                Content = s.Content,
                RequiresAcknowledgment = s.RequiresAcknowledgment
            }).OrderBy(s => s.SectionNumber).ToList(),
            Questions = entity.Questions.Select(q => new ToolboxTalkQuestionDto
            {
                Id = q.Id,
                ToolboxTalkId = q.ToolboxTalkId,
                QuestionNumber = q.QuestionNumber,
                QuestionText = q.QuestionText,
                QuestionType = q.QuestionType,
                QuestionTypeDisplay = GetQuestionTypeDisplay(q.QuestionType),
                Options = !string.IsNullOrEmpty(q.Options) ? JsonSerializer.Deserialize<List<string>>(q.Options) : null,
                CorrectAnswer = q.CorrectAnswer,
                Points = q.Points
            }).OrderBy(q => q.QuestionNumber).ToList()
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
}
