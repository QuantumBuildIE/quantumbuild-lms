namespace QuantumBuild.Core.Application.Features.Employees;

public interface ITargetEmployeeResolver
{
    Task<List<Guid>> ResolveEmployeeIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> departmentIds,
        IReadOnlyCollection<Guid> siteIds,
        CancellationToken cancellationToken);
}
