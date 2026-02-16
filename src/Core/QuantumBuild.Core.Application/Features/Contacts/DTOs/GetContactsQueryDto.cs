namespace QuantumBuild.Core.Application.Features.Contacts.DTOs;

public record GetContactsQueryDto(
    int PageNumber = 1,
    int PageSize = 20,
    string? SortColumn = null,
    string? SortDirection = null,
    string? Search = null,
    Guid? CompanyId = null
);
