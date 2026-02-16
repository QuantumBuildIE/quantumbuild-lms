using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Features.Roles;

namespace QuantumBuild.API.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    /// <summary>
    /// Get all roles (for dropdowns)
    /// </summary>
    /// <returns>List of roles</returns>
    [HttpGet]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _roleService.GetAllAsync();

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
