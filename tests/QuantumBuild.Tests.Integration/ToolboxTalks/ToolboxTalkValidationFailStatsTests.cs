using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for the ValidationFailStats aggregate on GetToolboxTalksQueryHandler
/// (Chunk 2 of the low-score external review feature). Covers the three-state display
/// (not validated / clean pass / N fails) and the "most recent run per language" dedup.
///
/// Runs and results are seeded directly via DbContext (self-contained scope, matching
/// UpdateToolboxTalkCommandHandlerTests.SeedTranslationAsync) since there is no API for
/// creating a TranslationValidationRun with an arbitrary Outcome. Talks are created via
/// AdminClient with unique titles used as the search term to isolate each test's row(s)
/// from other seeded/created data.
/// </summary>
[Collection("Integration")]
public class ToolboxTalkValidationFailStatsTests : IntegrationTestBase
{
    public ToolboxTalkValidationFailStatsTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static string UniqueTitle(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    private async Task<TalkResponseDto> CreateTalkAsync(string title)
    {
        var body = new
        {
            Title = title,
            Description = (string?)null,
            Frequency = ToolboxTalkFrequency.Once,
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", body);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TalkResponseDto>()
               ?? throw new InvalidOperationException("Create talk returned null");
    }

    /// <summary>
    /// Seeds a TranslationValidationRun with one TranslationValidationResult per entry in
    /// sectionOutcomes (SectionIndex 0..n-1). Uses a self-contained scope with an explicit
    /// TenantId, matching the SeedTranslationAsync pattern — there is no HTTP context here
    /// to auto-stamp the tenant.
    /// </summary>
    private async Task<Guid> SeedValidationRunAsync(
        Guid talkId, string languageCode, DateTime createdAt, params ValidationOutcome[] sectionOutcomes)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var runId = Guid.NewGuid();
        db.Set<TranslationValidationRun>().Add(new TranslationValidationRun
        {
            Id = runId,
            TenantId = TestTenantConstants.TenantId,
            ToolboxTalkId = talkId,
            LanguageCode = languageCode,
            SourceLanguage = "en",
            PassThreshold = 75,
            OverallScore = 80,
            OverallOutcome = ValidationOutcome.Pass,
            TotalSections = sectionOutcomes.Length,
            PassedSections = sectionOutcomes.Count(o => o == ValidationOutcome.Pass),
            ReviewSections = sectionOutcomes.Count(o => o == ValidationOutcome.Review),
            FailedSections = sectionOutcomes.Count(o => o == ValidationOutcome.Fail),
            Status = ValidationRunStatus.Completed,
            CreatedAt = createdAt,
            CreatedBy = "test-seeder"
        });

        for (var i = 0; i < sectionOutcomes.Length; i++)
        {
            db.Set<TranslationValidationResult>().Add(new TranslationValidationResult
            {
                Id = Guid.NewGuid(),
                ValidationRunId = runId,
                SectionIndex = i,
                SectionTitle = $"Section {i}",
                OriginalText = "Original text",
                TranslatedText = "Translated text",
                ScoreA = 80,
                ScoreB = 80,
                FinalScore = 80,
                RoundsUsed = 1,
                Outcome = sectionOutcomes[i],
                EngineOutcome = sectionOutcomes[i],
                EffectiveThreshold = 75,
                CreatedAt = createdAt,
                CreatedBy = "test-seeder"
            });
        }

        await db.SaveChangesAsync();
        return runId;
    }

    private async Task<ListItemDto> GetListItemAsync(Guid talkId, string searchTerm)
    {
        var response = await AdminClient.GetAsync(
            $"/api/toolbox-talks?searchTerm={Uri.EscapeDataString(searchTerm)}&pageSize=50");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<ListItemDto>>>();
        return result!.Data!.Items.Single(i => i.Id == talkId);
    }

    // ── tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoValidationRuns_HasValidationRunsFalse_ZeroCounts()
    {
        var title = UniqueTitle("FailStats_NoRuns");
        var talk = await CreateTalkAsync(title);

        var item = await GetListItemAsync(talk.Id, title);

        item.ValidationFailStats.HasValidationRuns.Should().BeFalse();
        item.ValidationFailStats.SectionFailCount.Should().Be(0);
        item.ValidationFailStats.FailingLanguageCount.Should().Be(0);
    }

    [Fact]
    public async Task OneRun_AllSectionsPass_ZeroFailsButHasRuns()
    {
        var title = UniqueTitle("FailStats_AllPass");
        var talk = await CreateTalkAsync(title);
        await SeedValidationRunAsync(talk.Id, "fr", DateTime.UtcNow.AddMinutes(-10),
            ValidationOutcome.Pass, ValidationOutcome.Pass);

        var item = await GetListItemAsync(talk.Id, title);

        item.ValidationFailStats.HasValidationRuns.Should().BeTrue();
        item.ValidationFailStats.SectionFailCount.Should().Be(0);
        item.ValidationFailStats.FailingLanguageCount.Should().Be(0);
    }

    [Fact]
    public async Task OneRun_SomeSectionsFail_CountsMatchFailingSections()
    {
        var title = UniqueTitle("FailStats_SomeFail");
        var talk = await CreateTalkAsync(title);
        await SeedValidationRunAsync(talk.Id, "fr", DateTime.UtcNow.AddMinutes(-10),
            ValidationOutcome.Pass, ValidationOutcome.Fail, ValidationOutcome.Fail, ValidationOutcome.Review);

        var item = await GetListItemAsync(talk.Id, title);

        item.ValidationFailStats.HasValidationRuns.Should().BeTrue();
        item.ValidationFailStats.SectionFailCount.Should().Be(2, "Review sections must not count as failures");
        item.ValidationFailStats.FailingLanguageCount.Should().Be(1);
    }

    [Fact]
    public async Task TwoLanguages_BothWithFailures_CountsSumAcrossLanguages()
    {
        var title = UniqueTitle("FailStats_TwoLangs");
        var talk = await CreateTalkAsync(title);
        await SeedValidationRunAsync(talk.Id, "fr", DateTime.UtcNow.AddMinutes(-10),
            ValidationOutcome.Fail, ValidationOutcome.Pass);
        await SeedValidationRunAsync(talk.Id, "de", DateTime.UtcNow.AddMinutes(-10),
            ValidationOutcome.Fail, ValidationOutcome.Fail);

        var item = await GetListItemAsync(talk.Id, title);

        item.ValidationFailStats.HasValidationRuns.Should().BeTrue();
        item.ValidationFailStats.SectionFailCount.Should().Be(3);
        item.ValidationFailStats.FailingLanguageCount.Should().Be(2);
    }

    [Fact]
    public async Task Revalidation_OnlyMostRecentRunPerLanguageCounts()
    {
        var title = UniqueTitle("FailStats_Revalidated");
        var talk = await CreateTalkAsync(title);

        // Older run: 5 failing sections
        await SeedValidationRunAsync(talk.Id, "fr", DateTime.UtcNow.AddHours(-2),
            ValidationOutcome.Fail, ValidationOutcome.Fail, ValidationOutcome.Fail,
            ValidationOutcome.Fail, ValidationOutcome.Fail);

        // Newer re-validation run: 1 failing section
        await SeedValidationRunAsync(talk.Id, "fr", DateTime.UtcNow.AddHours(-1),
            ValidationOutcome.Fail, ValidationOutcome.Pass);

        var item = await GetListItemAsync(talk.Id, title);

        item.ValidationFailStats.HasValidationRuns.Should().BeTrue();
        item.ValidationFailStats.SectionFailCount.Should().Be(1, "only the most recent run for the language should count");
        item.ValidationFailStats.FailingLanguageCount.Should().Be(1);
    }

    [Fact]
    public async Task MultipleTalks_BatchedAggregate_ReturnsCountsForAllNotJustFirst()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var talkA = await CreateTalkAsync($"FailStatsBatch_{token}_A");
        var talkB = await CreateTalkAsync($"FailStatsBatch_{token}_B");

        await SeedValidationRunAsync(talkA.Id, "fr", DateTime.UtcNow.AddMinutes(-10),
            ValidationOutcome.Fail, ValidationOutcome.Pass);
        await SeedValidationRunAsync(talkB.Id, "fr", DateTime.UtcNow.AddMinutes(-10),
            ValidationOutcome.Fail, ValidationOutcome.Fail, ValidationOutcome.Fail);

        var response = await AdminClient.GetAsync(
            $"/api/toolbox-talks?searchTerm={Uri.EscapeDataString($"FailStatsBatch_{token}")}&pageSize=50");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<ListItemDto>>>();

        var itemA = result!.Data!.Items.Single(i => i.Id == talkA.Id);
        var itemB = result.Data!.Items.Single(i => i.Id == talkB.Id);

        itemA.ValidationFailStats.SectionFailCount.Should().Be(1);
        itemB.ValidationFailStats.SectionFailCount.Should().Be(3);
    }

    #region Response DTOs

    private record TalkResponseDto(Guid Id, string Title);

    private record ValidationFailStatsDto(int SectionFailCount, int FailingLanguageCount, bool HasValidationRuns);

    private record ListItemDto(Guid Id, string Title, ValidationFailStatsDto ValidationFailStats);

    private record PaginatedResult<T>(
        List<T> Items,
        int PageNumber,
        int PageSize,
        int TotalCount,
        int TotalPages
    );

    private record ResultWrapper<T>(
        bool Success,
        T? Data,
        string? Message,
        List<string>? Errors
    );

    #endregion
}
