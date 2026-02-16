using Microsoft.AspNetCore.Authorization;

namespace QuantumBuild.Core.Infrastructure.Identity;

/// <summary>
/// Authorization attribute that requires a specific permission
/// </summary>
public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
        : base(policy: permission)
    {
    }
}
