using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalks;

public class GetToolboxTalksQueryHandler : IRequestHandler<GetToolboxTalksQuery, PaginatedList<ToolboxTalkListDto>>
{
    private readonly IToolboxTalksDbContext _context;
    private readonly ICoreDbContext _coreContext;

    public GetToolboxTalksQueryHandler(IToolboxTalksDbContext context, ICoreDbContext coreContext)
    {
        _context = context;
        _coreContext = coreContext;
    }

    public async Task<PaginatedList<ToolboxTalkListDto>> Handle(GetToolboxTalksQuery request, CancellationToken cancellationToken)
    {
        var query = _context.ToolboxTalks
            .Where(t => t.TenantId == request.TenantId && !t.IsDeleted)
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLower();
            query = query.Where(t =>
                t.Code.ToLower().Contains(searchLower) ||
                t.Title.ToLower().Contains(searchLower) ||
                (t.Description != null && t.Description.ToLower().Contains(searchLower)));
        }

        // Apply frequency filter
        if (request.Frequency.HasValue)
        {
            query = query.Where(t => t.Frequency == request.Frequency.Value);
        }

        // Apply active status filter
        if (request.IsActive.HasValue)
        {
            query = query.Where(t => t.IsActive == request.IsActive.Value);
        }

        // Apply talk status filter
        if (request.Status.HasValue)
        {
            query = query.Where(t => t.Status == request.Status.Value);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply ordering and pagination
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new ToolboxTalkListDto
            {
                Id = t.Id,
                Code = t.Code,
                Title = t.Title,
                Description = t.Description,
                Category = t.Category,
                Frequency = t.Frequency,
                FrequencyDisplay = GetFrequencyDisplay(t.Frequency),
                IsActive = t.IsActive,
                HasVideo = !string.IsNullOrEmpty(t.VideoUrl),
                RequiresQuiz = t.RequiresQuiz,
                Status = t.Status,
                StatusDisplay = GetStatusDisplay(t.Status),
                GeneratedFromVideo = t.GeneratedFromVideo,
                GeneratedFromPdf = t.GeneratedFromPdf,
                AutoAssignToNewEmployees = t.AutoAssignToNewEmployees,
                SectionCount = t.Sections.Count,
                QuestionCount = t.Questions.Count,
                CreatedAt = t.CreatedAt,
                CreatedBy = t.CreatedBy,
                LastEditedStep = t.LastEditedStep
            })
            .ToListAsync(cancellationToken);

        // Get completion stats for each talk
        var talkIds = items.Select(i => i.Id).ToList();
        var completionStats = await GetCompletionStats(talkIds, request.TenantId, cancellationToken);
        var validationFailStats = await GetValidationFailStats(talkIds, request.TenantId, cancellationToken);

        // Resolve creator display names via User table
        var creatorIds = items
            .Where(i => !string.IsNullOrEmpty(i.CreatedBy) && Guid.TryParse(i.CreatedBy, out _))
            .Select(i => Guid.Parse(i.CreatedBy))
            .Distinct()
            .ToList();

        var creatorUsers = await _coreContext.Users
            .Where(u => creatorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.UserName })
            .ToListAsync(cancellationToken);

        var creatorNames = creatorUsers.ToDictionary(
            u => u.Id.ToString(),
            u =>
            {
                var fullName = $"{u.FirstName} {u.LastName}".Trim();
                return fullName.Length > 0 ? fullName : (u.UserName ?? u.Id.ToString());
            });

        // Map completion stats and creator names to items
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var hasStats = completionStats.TryGetValue(item.Id, out var stats);
            var hasName = creatorNames.TryGetValue(item.CreatedBy, out var creatorName);
            var hasFailStats = validationFailStats.TryGetValue(item.Id, out var failStats);

            items[i] = item with
            {
                CompletionStats = hasStats ? stats : null,
                CreatedByName = hasName ? creatorName : null,
                ValidationFailStats = hasFailStats ? failStats! : new ToolboxTalkValidationFailStatsDto()
            };
        }

        return new PaginatedList<ToolboxTalkListDto>(items, totalCount, request.PageNumber, request.PageSize);
    }

    private async Task<Dictionary<Guid, ToolboxTalkCompletionStatsDto>> GetCompletionStats(
        List<Guid> talkIds,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var stats = await _context.ScheduledTalks
            .Where(st => talkIds.Contains(st.ToolboxTalkId) && st.TenantId == tenantId && !st.IsDeleted)
            .GroupBy(st => st.ToolboxTalkId)
            .Select(g => new
            {
                ToolboxTalkId = g.Key,
                TotalAssignments = g.Count(),
                CompletedCount = g.Count(st => st.Status == ScheduledTalkStatus.Completed),
                OverdueCount = g.Count(st => st.Status == ScheduledTalkStatus.Overdue ||
                    (st.Status != ScheduledTalkStatus.Completed && st.Status != ScheduledTalkStatus.Cancelled && st.DueDate < now)),
                PendingCount = g.Count(st => st.Status == ScheduledTalkStatus.Pending),
                InProgressCount = g.Count(st => st.Status == ScheduledTalkStatus.InProgress)
            })
            .ToListAsync(cancellationToken);

        return stats.ToDictionary(
            s => s.ToolboxTalkId,
            s => new ToolboxTalkCompletionStatsDto
            {
                TotalAssignments = s.TotalAssignments,
                CompletedCount = s.CompletedCount,
                OverdueCount = s.OverdueCount,
                PendingCount = s.PendingCount,
                InProgressCount = s.InProgressCount,
                CompletionRate = s.TotalAssignments > 0
                    ? Math.Round((decimal)s.CompletedCount / s.TotalAssignments * 100, 2)
                    : 0
            });
    }

    /// <summary>
    /// Computes section-language validation failure counts per talk, using only the most
    /// recent TranslationValidationRun per (talk, language) — there is no IsCurrent flag on
    /// the run entity, so "most recent" is derived from CreatedAt (ties broken by Id).
    /// Two queries total regardless of page size (run metadata, then failing result rows),
    /// matching the batched-aggregate pattern used by GetCompletionStats.
    /// </summary>
    private async Task<Dictionary<Guid, ToolboxTalkValidationFailStatsDto>> GetValidationFailStats(
        List<Guid> talkIds,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var runMeta = await _context.TranslationValidationRuns
            .Where(r => r.ToolboxTalkId != null
                && talkIds.Contains(r.ToolboxTalkId.Value)
                && r.TenantId == tenantId
                && !r.IsDeleted)
            .Select(r => new { r.Id, ToolboxTalkId = r.ToolboxTalkId!.Value, r.LanguageCode, r.CreatedAt })
            .ToListAsync(cancellationToken);

        if (runMeta.Count == 0)
        {
            return new Dictionary<Guid, ToolboxTalkValidationFailStatsDto>();
        }

        var latestRuns = runMeta
            .GroupBy(r => new { r.ToolboxTalkId, r.LanguageCode })
            .Select(g => g.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).First())
            .ToList();

        var latestRunIds = latestRuns.Select(r => r.Id).ToList();
        var runById = latestRuns.ToDictionary(r => r.Id);

        var failingRunIds = await _context.TranslationValidationResults
            .Where(res => latestRunIds.Contains(res.ValidationRunId) && res.Outcome == ValidationOutcome.Fail)
            .Select(res => res.ValidationRunId)
            .ToListAsync(cancellationToken);

        return latestRuns
            .Select(r => r.ToolboxTalkId)
            .Distinct()
            .ToDictionary(
                talkId => talkId,
                talkId =>
                {
                    var failingRunIdsForTalk = failingRunIds
                        .Where(runId => runById[runId].ToolboxTalkId == talkId)
                        .ToList();

                    return new ToolboxTalkValidationFailStatsDto
                    {
                        SectionFailCount = failingRunIdsForTalk.Count,
                        FailingLanguageCount = failingRunIdsForTalk
                            .Select(runId => runById[runId].LanguageCode)
                            .Distinct()
                            .Count(),
                        HasValidationRuns = true
                    };
                });
    }

    private static string GetFrequencyDisplay(ToolboxTalkFrequency frequency) => frequency switch
    {
        ToolboxTalkFrequency.Once => "One-time",
        ToolboxTalkFrequency.Weekly => "Weekly",
        ToolboxTalkFrequency.Monthly => "Monthly",
        ToolboxTalkFrequency.Annually => "Annually",
        _ => frequency.ToString()
    };

    private static string GetStatusDisplay(ToolboxTalkStatus status) => status switch
    {
        ToolboxTalkStatus.Draft => "Draft",
        ToolboxTalkStatus.Processing => "Processing",
        ToolboxTalkStatus.ReadyForReview => "Ready for Review",
        ToolboxTalkStatus.Published => "Published",
        _ => status.ToString()
    };
}
