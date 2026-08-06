using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Domain.Helpers;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.InitialiseToolboxTalk;

public class InitialiseToolboxTalkCommandHandler : IRequestHandler<InitialiseToolboxTalkCommand, ToolboxTalkDto>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly IValidator<InitialiseToolboxTalkCommand> _validator;

    public InitialiseToolboxTalkCommandHandler(
        IToolboxTalksDbContext dbContext,
        IValidator<InitialiseToolboxTalkCommand> validator)
    {
        _dbContext = dbContext;
        _validator = validator;
    }

    private static readonly HashSet<string> CommonWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "for", "in", "to", "of", "on", "at", "by", "with", "from", "is", "it"
    };

    public async Task<ToolboxTalkDto> Handle(InitialiseToolboxTalkCommand request, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        // Title uniqueness within tenant
        var titleExists = await _dbContext.ToolboxTalks
            .AnyAsync(t => t.TenantId == request.TenantId && t.Title == request.Title, cancellationToken);

        if (titleExists)
            throw new InvalidOperationException($"A learning with title '{request.Title}' already exists.");

        // Code — generate or validate
        string code;
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var codeExists = await _dbContext.ToolboxTalks
                .AnyAsync(t => t.TenantId == request.TenantId && t.Code == request.Code, cancellationToken);

            if (codeExists)
                throw new InvalidOperationException($"A learning with code '{request.Code}' already exists.");

            code = request.Code.Trim();
        }
        else
        {
            code = await GenerateCodeAsync(request.Title, request.TenantId, cancellationToken);
        }

        // DocumentRef — generate server-side when not supplied
        var documentRef = !string.IsNullOrWhiteSpace(request.DocumentRef)
            ? request.DocumentRef.Trim()
            : GenerateDocumentRef();

        // Load tenant defaults — null-coalescing fallbacks preserve current behaviour
        // when no settings row exists for this tenant.
        var tenantSettings = await _dbContext.ToolboxTalkSettings
            .Where(s => s.TenantId == request.TenantId && !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        // Map refresher string → RequiresRefresher + RefresherIntervalMonths
        var refresherFrequency = tenantSettings?.DefaultRefresherFrequency ?? "Once";
        var (defaultRequiresRefresher, defaultRefresherIntervalMonths) =
            RefresherFrequencyMapper.FromWizardFrequencyString(refresherFrequency);

        var talk = new ToolboxTalk
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Code = code,
            Title = request.Title,
            Description = request.Description,
            Category = request.Category,
            SourceLanguageCode = request.SourceLanguageCode,

            // Input mode (Step 1 choice)
            InputMode = request.InputMode,

            // Source file / text
            SourceFileUrl = request.SourceFileUrl,
            SourceFileName = request.SourceFileName,
            SourceFileType = request.SourceFileType,
            SourceText = request.SourceText,

            // PDF-specific fields — drives the Slideshow toggle visibility guard
            // (SettingsStep.tsx) and SlideshowGenerationService.GenerateFromPdfAsync,
            // both of which read PdfUrl rather than the generic SourceFileUrl.
            PdfUrl = request.InputMode == InputMode.Pdf ? request.SourceFileUrl : null,
            PdfFileName = request.InputMode == InputMode.Pdf ? request.SourceFileName : null,

            // Video URL
            VideoUrl = request.VideoUrl,
            VideoSource = request.VideoSource,

            // Target languages serialised as JSON array
            TargetLanguageCodes = JsonSerializer.Serialize(request.TargetLanguageCodes),

            // Audit metadata
            ReviewerName = request.ReviewerName,
            ReviewerOrg = request.ReviewerOrg,
            ReviewerRole = request.ReviewerRole,
            DocumentRef = documentRef,
            ClientName = request.ClientName,
            AuditPurpose = request.AuditPurpose,

            // Generation preferences
            AudienceRole = request.AudienceRole,
            // Explicit request value wins; otherwise fall back to the tenant default; otherwise
            // the initial system default. See ToolboxTalkSettings.DefaultPreserveSourceWording /
            // DefaultIncludeQuiz.
            PreserveSourceWording = request.PreserveSourceWording ?? tenantSettings?.DefaultPreserveSourceWording ?? true,
            RequiresQuiz = request.IncludeQuiz ?? tenantSettings?.DefaultIncludeQuiz ?? true,

            // Wizard shell defaults — sourced from tenant ToolboxTalkSettings
            Status = ToolboxTalkStatus.Draft,
            IsActive = tenantSettings?.DefaultIsActive ?? true,
            GenerateCertificate = tenantSettings?.DefaultGenerateCertificate ?? true,
            MinimumVideoWatchPercent = tenantSettings?.DefaultMinimumVideoWatchPercent ?? 90,
            AutoAssignToNewEmployees = tenantSettings?.DefaultAutoAssign ?? true,
            AutoAssignDueDays = tenantSettings?.DefaultAutoAssignDueDays ?? 14,
            ShuffleQuestions = tenantSettings?.DefaultShuffleQuestions ?? true,
            ShuffleOptions = tenantSettings?.DefaultShuffleOptions ?? true,
            // UseQuestionPool is deliberately NOT defaulted true: it changes which questions
            // each employee sees, which has compliance-assessment-consistency implications
            // (see audit chunk). Tenants who accept that tradeoff can opt in via the tenant
            // setting; the system-wide initial default stays false.
            UseQuestionPool = tenantSettings?.DefaultUseQuestionPool ?? false,
            AllowRetry = tenantSettings?.DefaultAllowRetry ?? true,
            GenerateSlidesFromPdf = tenantSettings?.DefaultGenerateSlideshow ?? false,
            PassingScore = tenantSettings?.DefaultPassingScore ?? 80,
            RequiresRefresher = defaultRequiresRefresher,
            RefresherIntervalMonths = defaultRefresherIntervalMonths,
            Frequency = RefresherFrequencyMapper.ToLegacyFrequency(defaultRequiresRefresher, defaultRefresherIntervalMonths),
            LastEditedStep = 1,
        };

        _dbContext.ToolboxTalks.Add(talk);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(talk);
    }

    /// <summary>
    /// Generates a document reference code: DOC-{base36(unix-seconds).ToUpper()}.
    /// </summary>
    private static string GenerateDocumentRef()
    {
        var seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return $"DOC-{ToBase36(seconds).ToUpperInvariant()}";
    }

    private static string ToBase36(long value)
    {
        const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
        if (value == 0) return "0";
        var result = new System.Text.StringBuilder();
        while (value > 0)
        {
            result.Insert(0, chars[(int)(value % 36)]);
            value /= 36;
        }
        return result.ToString();
    }

    private async Task<string> GenerateCodeAsync(string title, Guid tenantId, CancellationToken cancellationToken)
    {
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
            var cleaned = new string(title.Where(c => !char.IsWhiteSpace(c)).ToArray());
            prefix = cleaned.Length >= 3
                ? cleaned[..3].ToUpperInvariant()
                : cleaned.ToUpperInvariant();
        }

        if (prefix.Length > 16)
            prefix = prefix[..16];

        var pattern = prefix + "-";
        var existingCodes = await _dbContext.ToolboxTalks
            .IgnoreQueryFilters()
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
            FrequencyDisplay = entity.Frequency switch
            {
                ToolboxTalkFrequency.Once => "One-time",
                ToolboxTalkFrequency.Weekly => "Weekly",
                ToolboxTalkFrequency.Monthly => "Monthly",
                ToolboxTalkFrequency.Annually => "Annually",
                _ => entity.Frequency.ToString()
            },
            VideoUrl = entity.VideoUrl,
            VideoSource = entity.VideoSource,
            VideoSourceDisplay = entity.VideoSource == VideoSource.None ? "None" : entity.VideoSource.ToString(),
            AttachmentUrl = entity.AttachmentUrl,
            MinimumVideoWatchPercent = entity.MinimumVideoWatchPercent,
            RequiresQuiz = entity.RequiresQuiz,
            PassingScore = entity.PassingScore,
            IsActive = entity.IsActive,
            Status = entity.Status,
            StatusDisplay = "Draft",
            PdfUrl = entity.PdfUrl,
            PdfFileName = entity.PdfFileName,
            GeneratedFromVideo = entity.GeneratedFromVideo,
            GeneratedFromPdf = entity.GeneratedFromPdf,
            GenerateSlidesFromPdf = entity.GenerateSlidesFromPdf,
            SlidesGenerated = entity.SlidesGenerated,
            SlideCount = 0,
            QuizQuestionCount = entity.QuizQuestionCount,
            ShuffleQuestions = entity.ShuffleQuestions,
            ShuffleOptions = entity.ShuffleOptions,
            UseQuestionPool = entity.UseQuestionPool,
            IsPartOfCourse = entity.IsPartOfCourse,
            SourceLanguageCode = entity.SourceLanguageCode,
            AutoAssignToNewEmployees = entity.AutoAssignToNewEmployees,
            AutoAssignDueDays = entity.AutoAssignDueDays,
            GenerateCertificate = entity.GenerateCertificate,
            RequiresRefresher = entity.RequiresRefresher,
            RefresherIntervalMonths = entity.RefresherIntervalMonths,
            LastEditedStep = entity.LastEditedStep,

            // New wizard fields
            SourceFileUrl = entity.SourceFileUrl,
            SourceFileName = entity.SourceFileName,
            SourceFileType = entity.SourceFileType,
            SourceText = entity.SourceText,
            TargetLanguageCodes = entity.TargetLanguageCodes,
            ReviewerName = entity.ReviewerName,
            ReviewerOrg = entity.ReviewerOrg,
            ReviewerRole = entity.ReviewerRole,
            DocumentRef = entity.DocumentRef,
            ClientName = entity.ClientName,
            AuditPurpose = entity.AuditPurpose,
            AudienceRole = entity.AudienceRole,
            PreserveSourceWording = entity.PreserveSourceWording,
            InputMode = entity.InputMode,

            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Sections = new(),
            Questions = new(),
            Translations = new(),
        };
    }
}
