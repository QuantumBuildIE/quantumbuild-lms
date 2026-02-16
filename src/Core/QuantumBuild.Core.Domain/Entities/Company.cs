using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Core.Domain.Entities;

/// <summary>
/// Represents a company or client organization
/// </summary>
public class Company : TenantEntity
{
    /// <summary>
    /// Unique company code within the tenant
    /// </summary>
    public string CompanyCode { get; set; } = string.Empty;

    /// <summary>
    /// Company name
    /// </summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>
    /// Trading name (if different from company name)
    /// </summary>
    public string? TradingName { get; set; }

    /// <summary>
    /// Company registration number
    /// </summary>
    public string? RegistrationNumber { get; set; }

    /// <summary>
    /// VAT/Tax registration number
    /// </summary>
    public string? VatNumber { get; set; }

    /// <summary>
    /// Primary address line 1
    /// </summary>
    public string? AddressLine1 { get; set; }

    /// <summary>
    /// Primary address line 2
    /// </summary>
    public string? AddressLine2 { get; set; }

    /// <summary>
    /// City
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// County/State/Region
    /// </summary>
    public string? County { get; set; }

    /// <summary>
    /// Postal/ZIP code
    /// </summary>
    public string? PostalCode { get; set; }

    /// <summary>
    /// Country
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Main phone number
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Main email address
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Company website
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// Company status - true if active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Type of company (e.g., Client, Subcontractor, Partner)
    /// </summary>
    public string? CompanyType { get; set; }

    /// <summary>
    /// Additional notes about the company
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Navigation property for sites owned by this company
    /// </summary>
    public ICollection<Site> Sites { get; set; } = new List<Site>();

    /// <summary>
    /// Navigation property for contacts at this company
    /// </summary>
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
}
