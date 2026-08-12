namespace QuantumBuild.Core.Application.Features.Employees.DTOs;

/// <param name="DepartmentId">Optional department filter (ignored when DepartmentUnassigned is true)</param>
/// <param name="DepartmentUnassigned">When true, filter to employees with no department assigned</param>
/// <param name="SiteId">Optional site/location filter (ignored when SiteUnassigned is true)</param>
/// <param name="SiteUnassigned">When true, filter to employees with no site/location assigned</param>
public record GetEmployeesQueryDto(
    int PageNumber = 1,
    int PageSize = 20,
    string? SortColumn = null,
    string? SortDirection = null,
    string? Search = null,
    Guid? DepartmentId = null,
    bool DepartmentUnassigned = false,
    Guid? SiteId = null,
    bool SiteUnassigned = false
);
