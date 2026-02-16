namespace QuantumBuild.Core.Application.Features.Contacts.DTOs;

public record CreateContactDto(
    string FirstName,
    string LastName,
    string? JobTitle,
    string? Email,
    string? Phone,
    string? Mobile,
    Guid? CompanyId,
    Guid? SiteId,
    bool IsPrimaryContact,
    bool IsActive,
    string? Notes
);
