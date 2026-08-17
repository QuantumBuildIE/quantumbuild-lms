using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;

namespace QuantumBuild.Core.Application.Features.Employees;

public class TargetEmployeeResolver : ITargetEmployeeResolver
{
    private readonly ICoreDbContext _coreDbContext;

    public TargetEmployeeResolver(ICoreDbContext coreDbContext)
    {
        _coreDbContext = coreDbContext;
    }

    public async Task<List<Guid>> ResolveEmployeeIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> departmentIds,
        IReadOnlyCollection<Guid> siteIds,
        CancellationToken cancellationToken)
    {
        return await _coreDbContext.Employees
            .Where(e => e.TenantId == tenantId && e.IsActive && !e.IsDeleted
                && ((e.DepartmentId.HasValue && departmentIds.Contains(e.DepartmentId.Value))
                    || (e.PrimarySiteId.HasValue && siteIds.Contains(e.PrimarySiteId.Value))))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
    }
}
