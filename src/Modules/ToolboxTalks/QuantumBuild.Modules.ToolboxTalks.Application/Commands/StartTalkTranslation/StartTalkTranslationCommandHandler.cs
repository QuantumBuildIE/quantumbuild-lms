using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Translations;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.StartTalkTranslation;

public class StartTalkTranslationCommandHandler
    : IRequestHandler<StartTalkTranslationCommand, Result<StartTalkTranslationResult>>
{
    private readonly IToolboxTalksDbContext _context;
    private readonly ITranslationWorkflowService _workflow;
    private readonly ITranslationJobScheduler _jobScheduler;
    private readonly ILogger<StartTalkTranslationCommandHandler> _logger;

    public StartTalkTranslationCommandHandler(
        IToolboxTalksDbContext context,
        ITranslationWorkflowService workflow,
        ITranslationJobScheduler jobScheduler,
        ILogger<StartTalkTranslationCommandHandler> logger)
    {
        _context = context;
        _workflow = workflow;
        _jobScheduler = jobScheduler;
        _logger = logger;
    }

    public async Task<Result<StartTalkTranslationResult>> Handle(
        StartTalkTranslationCommand request,
        CancellationToken cancellationToken)
    {
        var talk = await _context.ToolboxTalks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                t => t.Id == request.TalkId && t.TenantId == request.TenantId && !t.IsDeleted,
                cancellationToken);

        if (talk is null)
            return Result.Fail<StartTalkTranslationResult>("Learning not found.");

        // Verify the requested language is in the talk's target language list
        if (!IsLanguageInTargets(talk.TargetLanguageCodes, request.LanguageCode))
            return Result.Fail<StartTalkTranslationResult>(
                $"Language '{request.LanguageCode}' is not in this learning's target language list.",
                FailureCode.WorkflowInvalidState);

        // Advance workflow state — blocks if state machine rejects (e.g. AwaitingThirdParty)
        var wfResult = await _workflow.StartTranslation(
            request.TalkId,
            request.LanguageCode,
            request.ConfirmOverwrite,
            TriggeredByType.User,
            ct: cancellationToken);

        if (!wfResult.Success)
            return Result.Fail<StartTalkTranslationResult>(
                wfResult.Errors.FirstOrDefault() ?? "Translation blocked by workflow state.",
                wfResult.ErrorCode.GetValueOrDefault());

        // Create the validation run (IsNewWizard=true skips the old-wizard session-relevance guard)
        var run = new TranslationValidationRun
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            ToolboxTalkId = request.TalkId,
            LanguageCode = request.LanguageCode,
            SectorKey = null,
            PassThreshold = 75,
            SourceLanguage = talk.SourceLanguageCode,
            ReviewerName = talk.ReviewerName,
            ReviewerOrg = talk.ReviewerOrg,
            ReviewerRole = talk.ReviewerRole,
            DocumentRef = talk.DocumentRef,
            ClientName = talk.ClientName,
            AuditPurpose = talk.AuditPurpose,
            Status = ValidationRunStatus.Pending,
            IsNewWizard = true
        };

        _context.TranslationValidationRuns.Add(run);
        await _context.SaveChangesAsync(cancellationToken);

        var jobId = _jobScheduler.EnqueueValidation(run.Id, request.TenantId);

        _logger.LogInformation(
            "StartTalkTranslation: talk {TalkId}, lang {Lang}, runId {RunId}, jobId {JobId}",
            request.TalkId, request.LanguageCode, run.Id, jobId);

        return Result.Ok(new StartTalkTranslationResult(run.Id, jobId));
    }

    private static bool IsLanguageInTargets(string? targetLanguageCodesJson, string languageCode)
    {
        if (string.IsNullOrWhiteSpace(targetLanguageCodesJson))
            return false;

        try
        {
            var codes = JsonSerializer.Deserialize<List<string>>(targetLanguageCodesJson);
            return codes?.Contains(languageCode, StringComparer.OrdinalIgnoreCase) == true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
