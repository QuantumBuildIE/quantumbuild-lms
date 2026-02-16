namespace QuantumBuild.Core.Application.Features.Employees.DTOs;

public record CreateEmployeeDto(
    /// <summary>
    /// Employee code is auto-generated on the backend (format: EMP001, EMP002, etc.).
    /// Any value sent from the frontend will be ignored.
    /// </summary>
    string? EmployeeCode,
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
    /// If true (default), creates a linked User account when Email is provided.
    /// The user will receive a password setup email.
    /// </summary>
    bool CreateUserAccount = true,
    /// <summary>
    /// Optional role name to assign to the created user.
    /// Defaults to "SiteManager" if not specified.
    /// Valid roles: Admin, Finance, OfficeStaff, SiteManager, WarehouseStaff
    /// </summary>
    string? UserRole = null,
    /// <summary>
    /// Preferred language for Toolbox Talk subtitles and notifications (ISO 639-1 code).
    /// Defaults to "en" (English) if not specified.
    /// </summary>
    string PreferredLanguage = "en",
    /// <summary>
    /// Float person ID - links this employee to a Float person record for schedule integration.
    /// When set manually, FloatLinkMethod will be set to "Manual".
    /// </summary>
    int? FloatPersonId = null
);
