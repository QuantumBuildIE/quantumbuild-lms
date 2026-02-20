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

    private static readonly HashSet<string> CommonWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "for", "in", "to", "of", "on", "at", "by", "with", "from", "is", "it"
    };

    public async Task<ToolboxTalkDto> Handle(CreateToolboxTalkCommand request, CancellationToken cancellationToken)
    {
        // Validate title is unique within tenant
        var titleExists = await _dbContext.ToolboxTalks
            .AnyAsync(t => t.TenantId == request.TenantId && t.Title == request.Title, cancellationToken);

        if (titleExists)
        {
            throw new InvalidOperationException($"A learning with title '{request.Title}' already exists.");
        }

        // Generate or validate the code
        string code;
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            // Validate uniqueness of provided code
            var codeExists = await _dbContext.ToolboxTalks
                .AnyAsync(t => t.TenantId == request.TenantId && t.Code == request.Code, cancellationToken);

            if (codeExists)
            {
                throw new InvalidOperationException($"A learning with code '{request.Code}' already exists.");
            }

            code = request.Code.Trim();
        }
        else
        {
            code = await GenerateCodeAsync(request.Title, request.TenantId, cancellationToken);
        }

        // Create the toolbox talk
        var toolboxTalk = new ToolboxTalk
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Code = code,
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
            Code = entity.Code,
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

    private async Task<string> GenerateCodeAsync(string title, Guid tenantId, CancellationToken cancellationToken)
    {
        // Strip common words and take first letter of each remaining word
        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !CommonWords.Contains(w))
            .ToList();

        string prefix;
        if (words.Count >= 2)
        {
            prefix = string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
        }
        else
        {
            // Fewer than 2 characters — take first 3 chars of title
            var cleaned = new string(title.Where(c => !char.IsWhiteSpace(c)).ToArray());
            prefix = cleaned.Length >= 3
                ? cleaned[..3].ToUpperInvariant()
                : cleaned.ToUpperInvariant();
        }

        // Truncate prefix if it would exceed max length with suffix (20 - 4 for "-NNN")
        if (prefix.Length > 16)
            prefix = prefix[..16];

        // Find existing codes with the same prefix to determine next number
        var pattern = prefix + "-";
        var existingCodes = await _dbContext.ToolboxTalks
            .Where(t => t.TenantId == tenantId && t.Code.StartsWith(pattern))
            .Select(t => t.Code)
            .ToListAsync(cancellationToken);

        var maxNumber = 0;
        foreach (var existingCode in existingCodes)
        {
            var suffix = existingCode[pattern.Length..];
            if (int.TryParse(suffix, out var number) && number > maxNumber)
                maxNumber = number;
        }

        return $"{prefix}-{(maxNumber + 1):D3}";
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
