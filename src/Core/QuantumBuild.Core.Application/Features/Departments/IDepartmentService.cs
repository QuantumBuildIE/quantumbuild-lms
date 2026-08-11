using QuantumBuild.Core.Application.Features.Departments.DTOs;
using QuantumBuild.Core.Application.Models;

namespace QuantumBuild.Core.Application.Features.Departments;

public interface IDepartmentService
{
    Task<Result<List<DepartmentDto>>> GetAllAsync();
    Task<Result<PaginatedList<DepartmentDto>>> GetPaginatedAsync(GetDepartmentsQueryDto query);
    Task<Result<DepartmentDto>> GetByIdAsync(Guid id);
    Task<Result<DepartmentDto>> CreateAsync(CreateDepartmentDto dto);
    Task<Result<DepartmentDto>> UpdateAsync(Guid id, UpdateDepartmentDto dto);
}
