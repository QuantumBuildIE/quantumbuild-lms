namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;

/// <summary>
/// Minimal admin/service-level mechanism to move a RegulatoryStructureMap between Draft and
/// Verified. This is deliberately NOT a review UI (a human working through every feature with
/// approve/edit is a later chunk) — just the state transition and its recording, plus the
/// reset-to-draft mechanism that guarantees a Verified stamp can never sit on since-changed
/// content.
/// </summary>
public interface IRegulatoryStructureMapVerificationService
{
    /// <summary>
    /// Marks the map Verified, recording who and when. Throws <see cref="InvalidOperationException"/>
    /// if no map exists with this id.
    /// </summary>
    Task VerifyAsync(
        Guid regulatoryStructureMapId,
        string verifiedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits one feature's verbatim text and/or footnote definition. If the owning map is
    /// currently Verified, this resets it to Draft and clears VerifiedBy/VerifiedAt in the same
    /// operation — a verified map can never retain its stamp once its content changes. Throws
    /// <see cref="InvalidOperationException"/> if no feature exists with this id.
    /// </summary>
    Task EditFeatureAsync(
        Guid regulatoryStructureMapFeatureId,
        string verbatimText,
        string? footnoteDefinition,
        CancellationToken cancellationToken = default);
}
