namespace QuantumBuild.Core.Application.Features.Employees.DTOs;

public record GetEmployeesQueryDto(
    int PageNumber = 1,
    int PageSize = 20,
    string? SortColumn = null,
    string? SortDirection = null,
    string? Search = null
);
