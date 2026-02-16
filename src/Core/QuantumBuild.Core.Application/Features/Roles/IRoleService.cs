using QuantumBuild.Core.Application.Features.Roles.DTOs;
using QuantumBuild.Core.Application.Models;

namespace QuantumBuild.Core.Application.Features.Roles;

public interface IRoleService
{
    Task<Result<List<RoleDto>>> GetAllAsync();
}
