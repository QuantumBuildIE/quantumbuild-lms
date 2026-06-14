using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.PublishToolboxTalk;

public class PublishToolboxTalkCommandHandler
    : IRequestHandler<PublishToolboxTalkCommand, Result<PublishTalkResult>>
{
    private readonly IToolboxTalksDbContext _dbContext;

    public PublishToolboxTalkCommandHandler(IToolboxTalksDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PublishTalkResult>> Handle(
        PublishToolboxTalkCommand request, CancellationToken ct)
    {
        var talk = await _dbContext.ToolboxTalks
            .Include(t => t.Sections.Where(s => !s.IsDeleted))
            .Where(t => t.Id == request.TalkId && t.TenantId == request.TenantId && !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (talk is null)
            return Result.Fail<PublishTalkResult>("Learning not found.");

        if (talk.Status == ToolboxTalkStatus.Published)
            return Result.Fail<PublishTalkResult>(
                "Learning is already published.", FailureCode.WorkflowInvalidState);

        if (talk.Sections.Count == 0)
            return Result.Fail<PublishTalkResult>(
                "Learning must have at least one section before publishing.",
                FailureCode.WorkflowInvalidState);

        // Reachability gate: if target languages are declared, at least one must have
        // a completed validation run (quality gate — English-only path skips this).
        var targetLanguageCodes = ParseTargetLanguageCodes(talk.TargetLanguageCodes);
        if (targetLanguageCodes.Count > 0)
        {
            var hasCompletedRun = await _dbContext.TranslationValidationRuns
                .AnyAsync(r => r.ToolboxTalkId == request.TalkId
                            && r.TenantId == request.TenantId
                            && r.Status == ValidationRunStatus.Completed
                            && !r.IsDeleted, ct);

            if (!hasCompletedRun)
                return Result.Fail<PublishTalkResult>(
                    "At least one translation must have a completed validation run before publishing.",
                    FailureCode.WorkflowInvalidState);
        }

        var publishedAt = DateTime.UtcNow;
        talk.Status = ToolboxTalkStatus.Published;
        talk.PublishedAt = publishedAt;
        talk.UpdatedAt = publishedAt;

        await _dbContext.SaveChangesAsync(ct);

        return Result.Ok(new PublishTalkResult(talk.Id, "Published", publishedAt));
    }

    private static List<string> ParseTargetLanguageCodes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
