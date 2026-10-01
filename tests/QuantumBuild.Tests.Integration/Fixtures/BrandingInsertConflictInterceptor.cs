using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Tests.Integration.Fixtures;

/// <summary>
/// Test-only: on the first save that inserts a TenantBranding, runs a one-shot callback (which inserts a
/// conflicting row through a separate DbContext) just before the insert hits the database, deterministically
/// reproducing a concurrent first upload. Disarms itself before invoking the callback.
/// </summary>
public class BrandingInsertConflictInterceptor : SaveChangesInterceptor
{
    private Func<Task>? _beforeInsert;

    public void Arm(Func<Task> beforeInsert) => _beforeInsert = beforeInsert;
    public void Disarm() => _beforeInsert = null;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var callback = _beforeInsert;
        if (callback != null && eventData.Context!.ChangeTracker.Entries<TenantBranding>()
                .Any(e => e.State == EntityState.Added))
        {
            _beforeInsert = null;
            await callback();
        }
        return result;
    }
}
