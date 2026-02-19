using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using QuantumBuild.Core.Application.Interfaces;

namespace QuantumBuild.Core.Infrastructure.Services;

/// <summary>
/// Implementation of ICurrentUserService that reads from JWT claims.
/// SuperUser users can override tenant via X-Tenant-Id header.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Current user's ID from JWT sub claim
    /// </summary>
    public string UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// <summary>
    /// Current user's name from JWT claims (combines given_name and family_name)
    /// </summary>
    public string UserName
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null)
                return string.Empty;

            var givenName = user.FindFirstValue(ClaimTypes.GivenName) ?? string.Empty;
            var familyName = user.FindFirstValue(ClaimTypes.Surname) ?? string.Empty;

            if (!string.IsNullOrEmpty(givenName) || !string.IsNullOrEmpty(familyName))
                return $"{givenName} {familyName}".Trim();

            // Fall back to email if name not available
            return user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        }
    }

    /// <summary>
    /// Whether the current user is a super user (from JWT claim)
    /// </summary>
    public bool IsSuperUser
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("is_super_user");
            return string.Equals(claim, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Current user's linked Employee ID from JWT claim (null if not linked)
    /// </summary>
    public Guid? EmployeeId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("employee_id");
            if (Guid.TryParse(claim, out var employeeId))
                return employeeId;
            return null;
        }
    }

    /// <summary>
    /// Current user's tenant ID.
    /// For SuperUser: reads X-Tenant-Id header, returns Guid.Empty if absent (bypasses tenant filter).
    /// For regular users: reads from JWT tenant_id claim.
    /// </summary>
    public Guid TenantId
    {
        get
        {
            if (IsSuperUser)
            {
                var headerValue = _httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault();
                if (!string.IsNullOrEmpty(headerValue) && Guid.TryParse(headerValue, out var headerTenantId))
                    return headerTenantId;

                // No header → Guid.Empty triggers tenant filter bypass
                return Guid.Empty;
            }

            var tenantIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("tenant_id");
            if (Guid.TryParse(tenantIdClaim, out var tenantId))
                return tenantId;

            return Guid.Empty;
        }
    }
}
