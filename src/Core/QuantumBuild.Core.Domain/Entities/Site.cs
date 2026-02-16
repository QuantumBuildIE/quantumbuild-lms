using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Core.Domain.Entities;

/// <summary>
/// Represents a construction site where stock can be ordered and delivered
/// </summary>
public class Site : TenantEntity
{
    /// <summary>
    /// Unique code for the site within the tenant
    /// </summary>
    public string SiteCode { get; set; } = string.Empty;

    /// <summary>
    /// Name of the construction site
    /// </summary>
    public string SiteName { get; set; } = string.Empty;

    /// <summary>
    /// Site address
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// City where the site is located
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// Postal/ZIP code
    /// </summary>
    public string? PostalCode { get; set; }

    /// <summary>
    /// Site manager's ID (FK to Employee)
    /// </summary>
    public Guid? SiteManagerId { get; set; }

    /// <summary>
    /// Site manager navigation property
    /// </summary>
    public Employee? SiteManager { get; set; }

    /// <summary>
    /// Company that owns/operates this site
    /// </summary>
    public Guid? CompanyId { get; set; }

    /// <summary>
    /// Company navigation property
    /// </summary>
    public Company? Company { get; set; }

    /// <summary>
    /// Contact phone number for the site
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Contact email for the site
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Site status - true if active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Additional notes about the site
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// GPS latitude for geofencing (WGS84 coordinate system)
    /// </summary>
    public decimal? Latitude { get; set; }

    /// <summary>
    /// GPS longitude for geofencing (WGS84 coordinate system)
    /// </summary>
    public decimal? Longitude { get; set; }

    /// <summary>
    /// Site-specific geofence radius in meters (overrides tenant default if set)
    /// </summary>
    public int? GeofenceRadiusMeters { get; set; }

    /// <summary>
    /// Float project ID - links this site to a Float project record
    /// </summary>
    public int? FloatProjectId { get; set; }

    /// <summary>
    /// When this site was linked to Float
    /// </summary>
    public DateTime? FloatLinkedAt { get; set; }

    /// <summary>
    /// How this site was linked to Float (Auto-Name, Manual)
    /// </summary>
    public string? FloatLinkMethod { get; set; }
}
