namespace QuantumBuild.Core.Application.Features.Companies.DTOs;

public record ContactSummaryDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string? JobTitle,
    string? Email,
    string? Phone,
    string? Mobile,
    bool IsPrimaryContact,
    bool IsActive
);

public record CompanyDto(
    Guid Id,
    string CompanyCode,
    string CompanyName,
    string? TradingName,
    string? RegistrationNumber,
    string? VatNumber,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? County,
    string? PostalCode,
    string? Country,
    string? Phone,
    string? Email,
    string? Website,
    string? CompanyType,
    bool IsActive,
    string? Notes,
    int ContactCount,
    List<ContactSummaryDto> Contacts
);
