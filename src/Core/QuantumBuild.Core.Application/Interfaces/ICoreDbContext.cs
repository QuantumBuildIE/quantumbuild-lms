using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Application.Interfaces;

/// <summary>
/// Interface for the Core database context with shared entities
/// </summary>
public interface ICoreDbContext
{
    // Identity DbSets
    DbSet<User> Users { get; }

    // Core DbSets
    DbSet<Tenant> Tenants { get; }
    DbSet<Site> Sites { get; }
    DbSet<Employee> Employees { get; }
    DbSet<Company> Companies { get; }
    DbSet<Contact> Contacts { get; }
    DbSet<SupervisorAssignment> SupervisorAssignments { get; }

    // Settings DbSets
    DbSet<TenantSetting> TenantSettings { get; }

    // Lookup DbSets
    DbSet<LookupCategory> LookupCategories { get; }
    DbSet<LookupValue> LookupValues { get; }
    DbSet<TenantLookupValue> TenantLookupValues { get; }

    /// <summary>
    /// Save changes to the database
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
