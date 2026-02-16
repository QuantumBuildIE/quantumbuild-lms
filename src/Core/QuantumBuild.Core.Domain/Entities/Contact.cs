using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Core.Domain.Entities;

/// <summary>
/// Represents a contact person associated with a company or site
/// </summary>
public class Contact : TenantEntity
{
    /// <summary>
    /// Contact's first name
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Contact's last name
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Full name (computed property)
    /// </summary>
    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Contact's job title
    /// </summary>
    public string? JobTitle { get; set; }

    /// <summary>
    /// Contact's email address
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Contact's phone number
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Contact's mobile number
    /// </summary>
    public string? Mobile { get; set; }

    /// <summary>
    /// Company this contact belongs to
    /// </summary>
    public Guid? CompanyId { get; set; }

    /// <summary>
    /// Company navigation property
    /// </summary>
    public Company? Company { get; set; }

    /// <summary>
    /// Site this contact is associated with
    /// </summary>
    public Guid? SiteId { get; set; }

    /// <summary>
    /// Site navigation property
    /// </summary>
    public Site? Site { get; set; }

    /// <summary>
    /// Whether this is the primary contact for the company/site
    /// </summary>
    public bool IsPrimaryContact { get; set; }

    /// <summary>
    /// Contact status - true if active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Additional notes about the contact
    /// </summary>
    public string? Notes { get; set; }
}
