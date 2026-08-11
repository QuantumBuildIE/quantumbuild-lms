namespace QuantumBuild.Core.Application.Features.Departments.DTOs;

public record GetDepartmentsQueryDto(
    int PageNumber = 1,
    int PageSize = 20,
    string? SortColumn = null,
    string? SortDirection = null,
    string? Search = null
);
