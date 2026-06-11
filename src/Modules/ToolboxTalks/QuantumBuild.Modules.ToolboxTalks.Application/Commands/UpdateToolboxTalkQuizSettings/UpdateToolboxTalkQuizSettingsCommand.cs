using MediatR;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalkQuizSettings;

/// <summary>
/// Writes the 7 quiz-settings fields for a wizard-drafted talk.
/// Used by Step 3 (Quiz) of the new learning-wizard via QuizSettingsPanel auto-save.
/// </summary>
public record UpdateToolboxTalkQuizSettingsCommand(
    Guid TalkId,
    Guid TenantId,
    bool RequiresQuiz,
    int PassingScore,
    int? QuizQuestionCount,
    bool ShuffleQuestions,
    bool ShuffleOptions,
    bool UseQuestionPool,
    bool AllowRetry) : IRequest<Result<ToolboxTalkDto>>;
