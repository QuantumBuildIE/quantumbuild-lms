namespace QuantumBuild.Core.Application.Features.Contacts.DTOs;

public record ContactDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string? JobTitle,
    string? Email,
    string? Phone,
    string? Mobile,
    Guid? CompanyId,
    string? CompanyName,
    Guid? SiteId,
    string? SiteName,
    bool IsPrimaryContact,
    bool IsActive,
    string? Notes
);
