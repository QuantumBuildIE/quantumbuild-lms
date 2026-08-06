namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Which block a RegulatoryRequirement's source feature belongs to. Person-experience features
/// describe what the person receiving the service experiences; provider features describe what
/// the service provider must have in place to meet the standard. The two blocks are independently
/// numbered per standard in the source document (e.g. both blocks can have a "1.1.1") — Block, not
/// FeatureIdentifier alone, disambiguates which one a given requirement transcribes. Also used by
/// the Application-layer structure map (see IRegulatoryStructureMapProvider) that this field's
/// values are ultimately sourced from.
/// </summary>
public enum RequirementBlock
{
    Person = 1,
    Provider = 2
}
