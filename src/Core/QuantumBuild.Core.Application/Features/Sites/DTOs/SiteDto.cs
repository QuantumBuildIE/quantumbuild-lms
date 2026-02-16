namespace QuantumBuild.Core.Application.Features.Sites.DTOs;

public record SiteDto(
    Guid Id,
    string SiteCode,
    string SiteName,
    string? Address,
    string? City,
    string? PostalCode,
    Guid? SiteManagerId,
    string? SiteManagerName,
    Guid? CompanyId,
    string? CompanyName,
    string? Phone,
    string? Email,
    bool IsActive,
    string? Notes,
    decimal? Latitude,
    decimal? Longitude,
    int? GeofenceRadiusMeters,
    int? FloatProjectId = null,
    DateTime? FloatLinkedAt = null,
    string? FloatLinkMethod = null
);
