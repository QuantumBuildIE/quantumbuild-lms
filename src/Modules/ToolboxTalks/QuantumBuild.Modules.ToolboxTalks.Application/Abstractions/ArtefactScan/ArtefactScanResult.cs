namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ArtefactScan;

public record ArtefactScanResult(IReadOnlyList<DetectedArtefact> Artefacts, bool HasArtefacts);
