namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;

/// <summary>One principle (e.g. Principle 1), holding every standard the document defines under it.</summary>
public sealed record StructurePrinciple(
    int Number,
    IReadOnlyList<StructureStandard> Standards);
