using Microsoft.AspNetCore.Authorization;

namespace QuantumBuild.Core.Infrastructure.Identity;

/// <summary>
/// Authorization handler that checks if user has required permission.
/// SuperUser users automatically pass all permission checks.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // SuperUser always has all permissions
        var isSuperUser = context.User.FindAll("is_super_user")
            .Any(c => string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase));

        if (isSuperUser)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Check if user has the required permission claim
        var permissionClaim = context.User.FindAll("permission")
            .Any(c => c.Value == requirement.Permission);

        if (permissionClaim)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
