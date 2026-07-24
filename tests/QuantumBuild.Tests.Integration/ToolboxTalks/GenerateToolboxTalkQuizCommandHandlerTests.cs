using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using System.Net;
using System.Text.Json.Serialization;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for POST /api/toolbox-talks/{id}/quiz/generate
/// (GenerateToolboxTalkQuizCommandHandler).
///
/// Scope: idempotency-guard regression coverage, mirroring
/// ParseToolboxTalkContentCommandHandlerTests's concurrent-call and failure-revert tests
/// (see that file's "concurrent-call idempotency guard tests" region). Prior to this file,
/// the Quiz handler had zero test coverage — its guard was correct by code review only
/// (see commit 171295e). These tests exercise the real handler via HTTP, so they fail if the
/// guard were ever removed.
///
/// Dispatch: AdminClient (Learnings.Manage) — same policy as the Parse endpoint.
/// Fakes: FakeContentParserService (to seed sections via /parse, a precondition for quiz
///        generation) + FakeAiQuizGenerationService (no Claude call).
/// </summary>
[Collection("Integration")]
public class GenerateToolboxTalkQuizCommandHandlerTests : IntegrationTestBase
{
    public GenerateToolboxTalkQuizCommandHandlerTests(CustomWebApplicationFactory factory)
        : base(factory) { }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string UniqueTitle(string prefix = "Quiz Test") =>
        $"{prefix} {Guid.NewGuid():N}"[..Math.Min(80, prefix.Length + 33)];

    /// <summary>Initialise a Draft ToolboxTalk via Step 1 and return its id.</summary>
    private async Task<InitTalkDto> InitialiseAsync(string title)
    {
        var request = new
        {
            Title = title,
            InputMode = "Text",
            SourceLanguageCode = "en",
            SourceText = "Always bend your knees when lifting heavy loads.",
            TargetLanguageCodes = new[] { "fr" },
            AudienceRole = "Operator",
            PreserveSourceWording = false,
            IncludeQuiz = true,
        };
        var json = System.Text.Json.JsonSerializer.Serialize(request,
            new System.Text.Json.JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            });
        var response = await AdminClient.PostAsync(
            "/api/toolbox-talks/initialise",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InitTalkDto>()
               ?? throw new InvalidOperationException("Initialise returned null");
    }

    /// <summary>
    /// Initialise a Draft talk and parse it (via the fake content parser) so it has at least
    /// one section — a precondition the Quiz handler enforces before it will run
    /// ("Cannot generate a quiz without sections. Parse content first.").
    /// </summary>
    private async Task<Guid> InitialiseWithSectionsAsync(string title)
    {
        var init = await InitialiseAsync(title);

        Factory.FakeContentParserService.NextSections =
        [
            new("Section 1: Introduction", "<p>Introduction content.</p>", 1),
        ];
        (await AdminClient.PostAsync($"/api/toolbox-talks/{init.Id}/parse", null))
            .EnsureSuccessStatusCode();

        return init.Id;
    }

    /// <summary>Force-set status in the DB to bypass business logic guards.</summary>
    private async Task SetStatusInDbAsync(Guid talkId, ToolboxTalkStatus status)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var talk = await db.Set<ToolboxTalk>()
            .IgnoreQueryFilters()
            .FirstAsync(t => t.Id == talkId);
        talk.Status = status;
        await db.SaveChangesAsync();
    }

    /// <summary>Read the talk's current status directly from the DB for assertion.</summary>
    private async Task<ToolboxTalkStatus> GetTalkStatusInDbAsync(Guid talkId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var talk = await db.Set<ToolboxTalk>()
            .IgnoreQueryFilters()
            .FirstAsync(t => t.Id == talkId);
        return talk.Status;
    }

    // ── concurrent-call idempotency guard tests ────────────────────────────────

    // A second call arriving while the talk is mid-quiz-generation (Status = Processing) is
    // rejected with 409, instead of firing a duplicate Claude call. Simulates the in-flight
    // window by forcing Processing in the DB — mirrors
    // ParseToolboxTalkContentCommandHandlerTests.TextMode_ConcurrentCallWhileProcessing_Returns409Conflict.
    [Fact]
    public async Task ConcurrentCallWhileProcessing_Returns409Conflict()
    {
        var talkId = await InitialiseWithSectionsAsync(UniqueTitle("Concurrent Quiz"));
        await SetStatusInDbAsync(talkId, ToolboxTalkStatus.Processing);

        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/quiz/generate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // The talk must remain in Processing — the guard rejects before touching state,
        // it does not clear the in-flight status of the call that is actually running.
        (await GetTalkStatusInDbAsync(talkId)).Should().Be(ToolboxTalkStatus.Processing);
    }

    // A failed quiz-generation call reverts Status to Draft rather than leaving the talk stuck
    // in Processing, so the user can retry without creating a new session. Also proves the
    // happy path end-to-end (the retry succeeds and returns the generated questions) — mirrors
    // ParseToolboxTalkContentCommandHandlerTests.TextMode_ParseFailure_RevertsToDraftAndAllowsRetry.
    [Fact]
    public async Task QuizGenerationFailure_RevertsToDraftAndAllowsRetry()
    {
        var talkId = await InitialiseWithSectionsAsync(UniqueTitle("Quiz Failure"));

        Factory.FakeAiQuizGenerationService.ShouldFail = true;
        var failResponse = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/quiz/generate", null);
        failResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await GetTalkStatusInDbAsync(talkId)).Should().Be(ToolboxTalkStatus.Draft);

        // Retry succeeds — status was not left stuck in Processing
        Factory.FakeAiQuizGenerationService.ShouldFail = false;
        Factory.FakeAiQuizGenerationService.NextQuestions =
        [
            new(1, "Retry question?", ["A", "B", "C", "D"], 2, ContentSource.Manual, false, null),
        ];
        var retryResponse = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/quiz/generate", null);
        retryResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await retryResponse.Content.ReadFromJsonAsync<TalkResult>();
        result!.Status.Should().Be("Draft");
        result.Questions.Should().ContainSingle(q => q.QuestionText == "Retry question?");

        (await GetTalkStatusInDbAsync(talkId)).Should().Be(ToolboxTalkStatus.Draft);
    }

    // ── local DTOs ────────────────────────────────────────────────────────────

    private record InitTalkDto(Guid Id, string Status);

    private record TalkResult(
        Guid Id,
        string Status,
        int? LastEditedStep,
        List<QuestionResult> Questions);

    private record QuestionResult(Guid Id, string QuestionText, int QuestionNumber);
}
