namespace QuantumBuild.Core.Application.Interfaces;

/// <summary>
/// Supplies an explicit tenant ID to <see cref="ICurrentUserService"/> when running inside a
/// Hangfire job's per-item DI scope, where there is no HttpContext to read a tenant from.
/// Registered scoped so each per-item scope (see CLAUDE.md Note 23) gets its own isolated
/// instance — a value set for one item's scope never leaks into another item's scope.
/// </summary>
public interface IJobTenantContextAccessor
{
    Guid? TenantId { get; set; }
}
