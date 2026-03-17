using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Sectors;

namespace QuantumBuild.API.Controllers;

[ApiController]
[Route("api/toolbox-talks/sectors")]
[Authorize]
public class SectorsController(ISectorService sectorService) : ControllerBase
{
    /// <summary>
    /// Get all active sectors (system-wide lookup, any authenticated user)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveSectors(CancellationToken cancellationToken)
    {
        var sectors = await sectorService.GetActiveSectorsAsync(cancellationToken);
        return Ok(sectors);
    }
}
