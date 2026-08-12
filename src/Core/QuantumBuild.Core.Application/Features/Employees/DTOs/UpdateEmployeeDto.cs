namespace QuantumBuild.Core.Application.Features.Employees.DTOs;

public record UpdateEmployeeDto(
    string EmployeeCode,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? Mobile,
    string? JobTitle,
    /// <summary>
    /// Legacy free-text department. No longer read by <c>EmployeeService.UpdateAsync</c>;
    /// kept on the DTO only for API-contract stability during the transition to
    /// <see cref="DepartmentId"/>, so existing per-employee free text values are never
    /// overwritten by an edit that doesn't touch department.
    /// </summary>
    string? Department,
    Guid? DepartmentId,
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
