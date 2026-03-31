namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.PreFlightScan;

public record PreFlightScanResult(
    IReadOnlyList<PreFlightFinding> Findings,
    bool HasFindings,
    int HighRiskCount,
    int ProperNounCount,
    int RoleConstructCount);
