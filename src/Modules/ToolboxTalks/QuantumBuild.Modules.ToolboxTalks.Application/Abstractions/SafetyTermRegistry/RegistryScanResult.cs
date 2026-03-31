namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.SafetyTermRegistry;

public record RegistryScanResult(
    IReadOnlyList<RegistryViolation> Violations,
    bool HasViolations);
