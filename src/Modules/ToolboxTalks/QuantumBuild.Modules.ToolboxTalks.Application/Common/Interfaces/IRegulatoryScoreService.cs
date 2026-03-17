using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;

/// <summary>
/// Service for scoring translation validation runs against regulatory standards.
/// Three score types: SourceDocument, PureTranslation, RegulatoryTranslation.
/// </summary>
public interface IRegulatoryScoreService
{
    Task<RegulatoryScoreResultDto> ScoreAsync(
        Guid validationRunId,
        ValidationScoreType scoreType,
        CancellationToken cancellationToken = default);

    Task<RegulatoryScoreHistoryDto> GetScoreHistoryAsync(
        Guid validationRunId,
        CancellationToken cancellationToken = default);
}
