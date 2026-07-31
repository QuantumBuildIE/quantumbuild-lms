namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Verification state of a RegulatoryStructureMap. New/generated maps start Draft. A map moves to
/// Verified only through an explicit verify action that records who and when; editing any of the
/// map's feature content resets it back to Draft so a "verified" stamp can never sit on
/// since-changed content.
/// </summary>
public enum RegulatoryStructureMapStatus
{
    Draft = 1,
    Verified = 2
}
