namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.SafetyTermRegistry;

public record RegistryViolation(
    string SourceTerm,
    string FoundBadPattern,
    string RequiredTerm,
    string Reason);
