using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Features.Departments.DTOs;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Application.Features.Departments;

public class DepartmentService : IDepartmentService
{
    private readonly ICoreDbContext _context;

    public DepartmentService(ICoreDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<DepartmentDto>>> GetAllAsync()
    {
        try
        {
            var departments = await _context.Departments
                .OrderBy(d => d.Name)
                .Select(d => new DepartmentDto(d.Id, d.Name, d.Code, d.IsActive))
                .ToListAsync();

            return Result.Ok(departments);
        }
        catch (Exception ex)
        {
            return Result.Fail<List<DepartmentDto>>($"Error retrieving departments: {ex.Message}");
        }
    }

    public async Task<Result<PaginatedList<DepartmentDto>>> GetPaginatedAsync(GetDepartmentsQueryDto query)
    {
        try
        {
            var departmentsQuery = _context.Departments.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var searchLower = query.Search.ToLower();
                departmentsQuery = departmentsQuery.Where(d =>
                    d.Name.ToLower().Contains(searchLower) ||
                    (d.Code != null && d.Code.ToLower().Contains(searchLower))
                );
            }

            departmentsQuery = ApplySorting(departmentsQuery, query.SortColumn, query.SortDirection);

            var totalCount = await departmentsQuery.CountAsync();

            var departments = await departmentsQuery
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(d => new DepartmentDto(d.Id, d.Name, d.Code, d.IsActive))
                .ToListAsync();

            var result = new PaginatedList<DepartmentDto>(departments, totalCount, query.PageNumber, query.PageSize);
            return Result.Ok(result);
        }
        catch (Exception ex)
        {
            return Result.Fail<PaginatedList<DepartmentDto>>($"Error retrieving departments: {ex.Message}");
        }
    }

    private static IQueryable<Department> ApplySorting(IQueryable<Department> query, string? sortColumn, string? sortDirection)
    {
        var isDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortColumn?.ToLower() switch
        {
            "name" => isDescending ? query.OrderByDescending(d => d.Name) : query.OrderBy(d => d.Name),
            "code" => isDescending ? query.OrderByDescending(d => d.Code) : query.OrderBy(d => d.Code),
            "isactive" => isDescending ? query.OrderByDescending(d => d.IsActive) : query.OrderBy(d => d.IsActive),
            _ => query.OrderBy(d => d.Name)
        };
    }

    public async Task<Result<DepartmentDto>> GetByIdAsync(Guid id)
    {
        try
        {
            var department = await _context.Departments
                .Where(d => d.Id == id)
                .Select(d => new DepartmentDto(d.Id, d.Name, d.Code, d.IsActive))
                .FirstOrDefaultAsync();

            if (department == null)
            {
                return Result.Fail<DepartmentDto>($"Department with ID {id} not found");
            }

            return Result.Ok(department);
        }
        catch (Exception ex)
        {
            return Result.Fail<DepartmentDto>($"Error retrieving department: {ex.Message}");
        }
    }

    public async Task<Result<DepartmentDto>> CreateAsync(CreateDepartmentDto dto)
    {
        try
        {
            var duplicateName = await _context.Departments
                .AnyAsync(d => d.Name == dto.Name);

            if (duplicateName)
            {
                return Result.Fail<DepartmentDto>($"Department with name '{dto.Name}' already exists");
            }

            var department = new Department
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Code = dto.Code,
                IsActive = dto.IsActive
            };

            _context.Departments.Add(department);
            await _context.SaveChangesAsync();

            return Result.Ok(new DepartmentDto(department.Id, department.Name, department.Code, department.IsActive));
        }
        catch (Exception ex)
        {
            return Result.Fail<DepartmentDto>($"Error creating department: {ex.Message}");
        }
    }

    public async Task<Result<DepartmentDto>> UpdateAsync(Guid id, UpdateDepartmentDto dto)
    {
        try
        {
            var department = await _context.Departments.FirstOrDefaultAsync(d => d.Id == id);

            if (department == null)
            {
                return Result.Fail<DepartmentDto>($"Department with ID {id} not found");
            }

            var duplicateName = await _context.Departments
                .AnyAsync(d => d.Name == dto.Name && d.Id != id);

            if (duplicateName)
            {
                return Result.Fail<DepartmentDto>($"Department with name '{dto.Name}' already exists");
            }

            department.Name = dto.Name;
            department.Code = dto.Code;
            department.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();

            return Result.Ok(new DepartmentDto(department.Id, department.Name, department.Code, department.IsActive));
        }
        catch (Exception ex)
        {
            return Result.Fail<DepartmentDto>($"Error updating department: {ex.Message}");
        }
    }
}
