namespace QuantumBuild.Core.Application.Features.Companies.DTOs;

public record GetCompaniesQueryDto(
    int PageNumber = 1,
    int PageSize = 20,
    string? SortColumn = null,
    string? SortDirection = null,
    string? Search = null,
    string? CompanyType = null
);
