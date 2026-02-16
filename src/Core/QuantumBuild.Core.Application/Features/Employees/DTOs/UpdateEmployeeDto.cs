namespace QuantumBuild.Core.Application.Features.Employees.DTOs;

public record UpdateEmployeeDto(
    string EmployeeCode,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? Mobile,
    string? JobTitle,
    string? Department,
    Guid? PrimarySiteId,
    DateTime? StartDate,
    DateTime? EndDate,
    bool IsActive,
    string? Notes,
    /// <summary>
    /// Geo tracker device ID for mobile geofence app integration (format: EVT####, e.g., "EVT0011")
    /// </summary>
    string? GeoTrackerID = null,
    /// <summary>
    /// Preferred language for Toolbox Talk subtitles and notifications (ISO 639-1 code).
    /// </summary>
    string? PreferredLanguage = null,
    /// <summary>
    /// Float person ID - links this employee to a Float person record for schedule integration.
    /// When set manually, FloatLinkMethod will be set to "Manual".
    /// When cleared (set to null), FloatLinkedAt and FloatLinkMethod will also be cleared.
    /// </summary>
    int? FloatPersonId = null
);
