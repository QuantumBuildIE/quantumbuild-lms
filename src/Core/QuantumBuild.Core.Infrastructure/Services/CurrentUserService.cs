using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using QuantumBuild.Core.Application.Interfaces;

namespace QuantumBuild.Core.Infrastructure.Services;

/// <summary>
/// Implementation of ICurrentUserService that reads from JWT claims
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
    /// Current user's tenant ID from JWT tenant_id claim
    /// </summary>
    public Guid TenantId
    {
        get
        {
            var tenantIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("tenant_id");

            if (Guid.TryParse(tenantIdClaim, out var tenantId))
                return tenantId;

            return Guid.Empty;
        }
    }
}
