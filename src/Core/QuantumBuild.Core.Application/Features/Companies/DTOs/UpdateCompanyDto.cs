namespace QuantumBuild.Core.Application.Features.Companies.DTOs;

public record UpdateCompanyDto(
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
    string? Notes
);
