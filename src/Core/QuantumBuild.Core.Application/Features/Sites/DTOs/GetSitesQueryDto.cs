namespace QuantumBuild.Core.Application.Features.Sites.DTOs;

public record GetSitesQueryDto(
    int PageNumber = 1,
    int PageSize = 20,
    string? SortColumn = null,
    string? SortDirection = null,
    string? Search = null
);
