using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Features.Roles.DTOs;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Application.Features.Roles;

public class RoleService : IRoleService
{
    private readonly RoleManager<Role> _roleManager;

    public RoleService(RoleManager<Role> roleManager)
    {
        _roleManager = roleManager;
    }

    public async Task<Result<List<RoleDto>>> GetAllAsync()
    {
        try
        {
            var roles = await _roleManager.Roles
                .Where(r => r.IsActive)
                .Include(r => r.RolePermissions)
                .OrderBy(r => r.Name)
                .Select(r => new RoleDto(
                    r.Id,
                    r.Name!,
                    r.Description,
                    r.RolePermissions.Count
                ))
                .ToListAsync();

            return Result.Ok(roles);
        }
        catch (Exception ex)
        {
            return Result.Fail<List<RoleDto>>($"Error retrieving roles: {ex.Message}");
        }
    }
}
