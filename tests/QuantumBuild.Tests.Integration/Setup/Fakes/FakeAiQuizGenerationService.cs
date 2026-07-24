using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.Setup.Fakes;

/// <summary>
/// Fake IAiQuizGenerationService that returns deterministic questions without calling the
/// Claude API. Mirrors FakeContentParserService's NextX/ShouldFail pattern (see
/// FakeContentCreationServices.cs) so GenerateToolboxTalkQuizCommandHandlerTests can exercise
/// both the happy path and the failure/idempotency-revert path deterministically.
/// </summary>
public class FakeAiQuizGenerationService : IAiQuizGenerationService
{
    public List<GeneratedQuizQuestion> NextQuestions { get; set; } =
    [
        new(1, "What is the first rule?", ["A", "B", "C", "D"], 0, ContentSource.Manual, false, null),
        new(2, "What is the second rule?", ["A", "B", "C", "D"], 1, ContentSource.Manual, false, null),
    ];

    public bool ShouldFail { get; set; } = false;

    public string NextErrorMessage { get; set; } = "Fake quiz generation failure";

    public Task<QuizGenerationResult> GenerateQuizAsync(
        Guid toolboxTalkId,
        string combinedContent,
        string? videoFinalPortionContent,
        bool hasVideoContent,
        bool hasPdfContent,
        Guid tenantId,
        Guid? userId = null,
        int minimumQuestions = 5,
        string audienceRole = "Operator",
        CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
            return Task.FromResult(new QuizGenerationResult(false, [], NextErrorMessage, 0, false));

        return Task.FromResult(new QuizGenerationResult(true, NextQuestions, null, 0, false));
    }
}
