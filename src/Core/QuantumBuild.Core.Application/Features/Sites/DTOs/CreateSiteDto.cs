namespace QuantumBuild.Core.Application.Features.Sites.DTOs;

public record CreateSiteDto(
    string SiteCode,
    string SiteName,
    string? Address,
    string? City,
    string? PostalCode,
    Guid? SiteManagerId,
    Guid? CompanyId,
    string? Phone,
    string? Email,
    bool IsActive,
    string? Notes,
    decimal? Latitude,
    decimal? Longitude,
    int? GeofenceRadiusMeters,
    int? FloatProjectId = null
);
