using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Regulatory;

/// <summary>
/// See IRegulatoryStructureMapVerificationService — the minimal draft/verified state transition,
/// plus the reset-on-edit mechanism.
/// </summary>
public sealed class RegulatoryStructureMapVerificationService : IRegulatoryStructureMapVerificationService
{
    private readonly IToolboxTalksDbContext _dbContext;

    public RegulatoryStructureMapVerificationService(IToolboxTalksDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task VerifyAsync(
        Guid regulatoryStructureMapId,
        string verifiedBy,
        CancellationToken cancellationToken = default)
    {
        var map = await _dbContext.RegulatoryStructureMaps
            .FirstOrDefaultAsync(m => m.Id == regulatoryStructureMapId, cancellationToken)
            ?? throw new InvalidOperationException($"RegulatoryStructureMap {regulatoryStructureMapId} not found");

        map.Status = RegulatoryStructureMapStatus.Verified;
        map.VerifiedBy = verifiedBy;
        map.VerifiedAt = DateTimeOffset.UtcNow;
        map.UpdatedAt = DateTime.UtcNow;
        map.UpdatedBy = verifiedBy;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task EditFeatureAsync(
        Guid regulatoryStructureMapFeatureId,
        string verbatimText,
        string? footnoteDefinition,
        CancellationToken cancellationToken = default)
    {
        var feature = await _dbContext.RegulatoryStructureMapFeatures
            .FirstOrDefaultAsync(f => f.Id == regulatoryStructureMapFeatureId, cancellationToken)
            ?? throw new InvalidOperationException($"RegulatoryStructureMapFeature {regulatoryStructureMapFeatureId} not found");

        feature.VerbatimText = verbatimText;
        feature.FootnoteDefinition = footnoteDefinition;

        var standard = await _dbContext.RegulatoryStructureMapStandards
            .FirstOrDefaultAsync(s => s.Id == feature.RegulatoryStructureMapStandardId, cancellationToken)
            ?? throw new InvalidOperationException($"RegulatoryStructureMapStandard {feature.RegulatoryStructureMapStandardId} not found");

        var principle = await _dbContext.RegulatoryStructureMapPrinciples
            .FirstOrDefaultAsync(p => p.Id == standard.RegulatoryStructureMapPrincipleId, cancellationToken)
            ?? throw new InvalidOperationException($"RegulatoryStructureMapPrinciple {standard.RegulatoryStructureMapPrincipleId} not found");

        var map = await _dbContext.RegulatoryStructureMaps
            .FirstOrDefaultAsync(m => m.Id == principle.RegulatoryStructureMapId, cancellationToken)
            ?? throw new InvalidOperationException($"RegulatoryStructureMap {principle.RegulatoryStructureMapId} not found");

        // The reset-to-draft mechanism: a Verified stamp can never survive a content edit.
        if (map.Status == RegulatoryStructureMapStatus.Verified)
        {
            map.Status = RegulatoryStructureMapStatus.Draft;
            map.VerifiedBy = null;
            map.VerifiedAt = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
