namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.PreFlightScan;

public record PreFlightFinding(
    PreFlightFindingType Type,
    string Term,
    string Risk,
    string? SuggestedTranslation);
