using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.LessonParser.Application.Common.Interfaces;
using QuantumBuild.Modules.LessonParser.Domain.Entities;
using QuantumBuild.Modules.LessonParser.Infrastructure.Persistence.Configurations;

namespace QuantumBuild.Modules.LessonParser.Infrastructure.Persistence;

/// <summary>
/// Database context for the Lesson Parser module.
/// Shares the same PostgreSQL database as the main ApplicationDbContext.
/// Implements multi-tenancy, soft deletes, and audit trail.
/// </summary>
public class LessonParserDbContext : DbContext, ILessonParserDbContext
{
    private readonly ICurrentUserService _currentUserService;

    public LessonParserDbContext(
        DbContextOptions<LessonParserDbContext> options,
        ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Current tenant ID for query filtering
    /// </summary>
    public Guid TenantId => _currentUserService?.TenantId ?? Guid.Empty;

    /// <summary>
    /// When true, tenant query filters are bypassed (SuperUser with no tenant selected)
    /// </summary>
    public bool BypassTenantFilter => _currentUserService?.IsSuperUser == true && TenantId == Guid.Empty;

    /// <summary>
    /// Current user ID for audit fields
    /// </summary>
    public string CurrentUserId => string.IsNullOrEmpty(_currentUserService?.UserId) ? "system" : _currentUserService.UserId;

    // Lesson Parser DbSets
    public DbSet<ParseJob> ParseJobs => Set<ParseJob>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply Lesson Parser entity configurations
        modelBuilder.ApplyConfiguration(new ParseJobConfiguration());

        // Apply global query filters
        modelBuilder.Entity<ParseJob>().HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        SetAuditFields();
        return base.SaveChanges();
    }

    private void SetAuditFields()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();
        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = CurrentUserId;
                    entry.Entity.IsDeleted = false;

                    // Set TenantId for new tenant entities
                    if (entry.Entity is TenantEntity tenantEntity && tenantEntity.TenantId == Guid.Empty)
                    {
                        tenantEntity.TenantId = TenantId;
                    }
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = CurrentUserId;

                    // Prevent modification of CreatedAt and CreatedBy
                    entry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(BaseEntity.CreatedBy)).IsModified = false;

                    // Prevent modification of TenantId
                    if (entry.Entity is TenantEntity)
                    {
                        entry.Property(nameof(TenantEntity.TenantId)).IsModified = false;
                    }
                    break;

                case EntityState.Deleted:
                    // Implement soft delete
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = CurrentUserId;
                    break;
            }
        }
    }
}
