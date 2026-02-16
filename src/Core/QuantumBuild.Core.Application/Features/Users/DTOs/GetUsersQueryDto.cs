namespace QuantumBuild.Core.Application.Features.Users.DTOs;

public record GetUsersQueryDto(
    int PageNumber = 1,
    int PageSize = 20,
    string? SortColumn = null,
    string? SortDirection = null,
    string? Search = null
);
