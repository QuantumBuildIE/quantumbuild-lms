using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Core.Domain.Entities;

/// <summary>
/// Records a tenant admin's acceptance of the Data Processing Agreement
/// </summary>
public class DpaAcceptance : BaseEntity
{
    /// <summary>
    /// Tenant that accepted the DPA
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// User who accepted the DPA on behalf of the tenant
    /// </summary>
    public Guid AcceptedByUserId { get; set; }

    /// <summary>
    /// Legal name of the organisation accepting the DPA
    /// </summary>
    public string OrganisationLegalName { get; set; } = string.Empty;

    /// <summary>
    /// Full name of the signatory
    /// </summary>
    public string SignatoryFullName { get; set; } = string.Empty;

    /// <summary>
    /// Role/title of the signatory within the organisation
    /// </summary>
    public string SignatoryRole { get; set; } = string.Empty;

    /// <summary>
    /// Company registration number (optional)
    /// </summary>
    public string? CompanyRegistrationNo { get; set; }

    /// <summary>
    /// Country of the organisation
    /// </summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>
    /// IP address of the user at the time of acceptance
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the DPA was accepted
    /// </summary>
    public DateTime AcceptedAt { get; set; }

    /// <summary>
    /// Version of the DPA that was accepted
    /// </summary>
    public string DpaVersion { get; set; } = string.Empty;

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public User AcceptedByUser { get; set; } = null!;
}
