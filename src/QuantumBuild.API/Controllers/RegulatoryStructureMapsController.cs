using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// SuperUser-only structure map administration. Minimal by design: no create/read/edit and no
/// review UI — just the Draft -> Verified transition via IRegulatoryStructureMapVerificationService.
/// A dedicated controller (not RegulatoryIngestionController) per CLAUDE.md note 24.
/// Returns the DTO directly (not a Result envelope).
/// </summary>
[ApiController]
[Route("api/regulatory/structure-maps")]
[Authorize]
public class RegulatoryStructureMapsController : ControllerBase
{
    private readonly ICurrentUserService _currentUser;
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly IRegulatoryStructureMapVerificationService _verificationService;

    public RegulatoryStructureMapsController(
        ICurrentUserService currentUser,
        IToolboxTalksDbContext dbContext,
        IRegulatoryStructureMapVerificationService verificationService)
    {
        _currentUser = currentUser;
        _dbContext = dbContext;
        _verificationService = verificationService;
    }

    /// <summary>
    /// Marks a structure map Verified, recording the verifier and timestamp. Idempotent: an
    /// already-Verified map is returned unchanged (the original verifier/timestamp are kept).
    /// </summary>
    [HttpPost("{mapId:guid}/verify")]
    [ProducesResponseType(typeof(StructureMapVerificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Verify(Guid mapId, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsSuperUser)
            return Forbid();

        var map = await _dbContext.RegulatoryStructureMaps
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == mapId, cancellationToken);

        if (map == null)
            return NotFound();

        if (map.Status != RegulatoryStructureMapStatus.Verified)
        {
            var verifier = string.IsNullOrWhiteSpace(_currentUser.UserName)
                ? _currentUser.UserId
                : _currentUser.UserName;

            await _verificationService.VerifyAsync(mapId, verifier, cancellationToken);

            map = await _dbContext.RegulatoryStructureMaps
                .AsNoTracking()
                .FirstAsync(m => m.Id == mapId, cancellationToken);
        }

        return Ok(new StructureMapVerificationDto(
            map.Id,
            map.RegulatoryDocumentId,
            map.Status.ToString(),
            map.VerifiedBy,
            map.VerifiedAt));
    }
}

public sealed record StructureMapVerificationDto(
    Guid Id,
    Guid RegulatoryDocumentId,
    string Status,
    string? VerifiedBy,
    DateTimeOffset? VerifiedAt);
