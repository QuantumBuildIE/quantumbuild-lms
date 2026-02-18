using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Infrastructure.Data.Configurations;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Configurations;

namespace QuantumBuild.Core.Infrastructure.Data;

/// <summary>
/// Main database context for the QuantumBuild LMS
/// Implements multi-tenancy, soft deletes, audit trail, and Identity
/// </summary>
public class ApplicationDbContext : IdentityDbContext<User, Role, Guid, IdentityUserClaim<Guid>, UserRole, IdentityUserLogin<Guid>, IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>, IToolboxTalksDbContext, ICoreDbContext
{
    private readonly ICurrentUserService _currentUserService;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
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

    // Core DbSets
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Contact> Contacts => Set<Contact>();

    // Lookup DbSets
    public DbSet<LookupCategory> LookupCategories => Set<LookupCategory>();
    public DbSet<LookupValue> LookupValues => Set<LookupValue>();
    public DbSet<TenantLookupValue> TenantLookupValues => Set<TenantLookupValue>();

    // Identity/Authorization DbSets
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    // Toolbox Talks DbSets
    public DbSet<ToolboxTalk> ToolboxTalks => Set<ToolboxTalk>();
    public DbSet<ToolboxTalkSection> ToolboxTalkSections => Set<ToolboxTalkSection>();
    public DbSet<ToolboxTalkQuestion> ToolboxTalkQuestions => Set<ToolboxTalkQuestion>();
    public DbSet<ToolboxTalkSchedule> ToolboxTalkSchedules => Set<ToolboxTalkSchedule>();
    public DbSet<ToolboxTalkScheduleAssignment> ToolboxTalkScheduleAssignments => Set<ToolboxTalkScheduleAssignment>();
    public DbSet<ScheduledTalk> ScheduledTalks => Set<ScheduledTalk>();
    public DbSet<ScheduledTalkSectionProgress> ScheduledTalkSectionProgress => Set<ScheduledTalkSectionProgress>();
    public DbSet<ScheduledTalkQuizAttempt> ScheduledTalkQuizAttempts => Set<ScheduledTalkQuizAttempt>();
    public DbSet<ScheduledTalkCompletion> ScheduledTalkCompletions => Set<ScheduledTalkCompletion>();
    public DbSet<ToolboxTalkSettings> ToolboxTalkSettings => Set<ToolboxTalkSettings>();
    public DbSet<ToolboxTalkTranslation> ToolboxTalkTranslations => Set<ToolboxTalkTranslation>();
    public DbSet<ToolboxTalkVideoTranslation> ToolboxTalkVideoTranslations => Set<ToolboxTalkVideoTranslation>();
    public DbSet<ToolboxTalkCourse> ToolboxTalkCourses => Set<ToolboxTalkCourse>();
    public DbSet<ToolboxTalkCourseItem> ToolboxTalkCourseItems => Set<ToolboxTalkCourseItem>();
    public DbSet<ToolboxTalkCourseTranslation> ToolboxTalkCourseTranslations => Set<ToolboxTalkCourseTranslation>();
    public DbSet<ToolboxTalkCourseAssignment> ToolboxTalkCourseAssignments => Set<ToolboxTalkCourseAssignment>();
    public DbSet<ToolboxTalkCertificate> ToolboxTalkCertificates => Set<ToolboxTalkCertificate>();
    public DbSet<ToolboxTalkSlide> ToolboxTalkSlides => Set<ToolboxTalkSlide>();
    public DbSet<ToolboxTalkSlideTranslation> ToolboxTalkSlideTranslations => Set<ToolboxTalkSlideTranslation>();
    public DbSet<ToolboxTalkSlideshowTranslation> ToolboxTalkSlideshowTranslations => Set<ToolboxTalkSlideshowTranslation>();
    public DbSet<SubtitleProcessingJob> SubtitleProcessingJobs => Set<SubtitleProcessingJob>();
    public DbSet<SubtitleTranslation> SubtitleTranslations => Set<SubtitleTranslation>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Identity tables with custom names
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.RefreshToken).HasMaxLength(500);
            entity.Ignore(e => e.FullName);

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.UserRoles)
                .WithOne(e => e.User)
                .HasForeignKey(ur => ur.UserId)
                .IsRequired();
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.Property(e => e.Description).HasMaxLength(500);

            entity.HasMany(e => e.UserRoles)
                .WithOne(e => e.Role)
                .HasForeignKey(ur => ur.RoleId)
                .IsRequired();

            entity.HasMany(e => e.RolePermissions)
                .WithOne(e => e.Role)
                .HasForeignKey(rp => rp.RoleId)
                .IsRequired();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");
        });

        modelBuilder.Entity<IdentityUserClaim<Guid>>(entity =>
        {
            entity.ToTable("UserClaims");
        });

        modelBuilder.Entity<IdentityUserLogin<Guid>>(entity =>
        {
            entity.ToTable("UserLogins");
        });

        modelBuilder.Entity<IdentityRoleClaim<Guid>>(entity =>
        {
            entity.ToTable("RoleClaims");
        });

        modelBuilder.Entity<IdentityUserToken<Guid>>(entity =>
        {
            entity.ToTable("UserTokens");
        });

        // Configure Permission entity
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("Permissions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Module).HasMaxLength(100).IsRequired();

            entity.HasIndex(e => e.Name).IsUnique();

            entity.HasMany(e => e.RolePermissions)
                .WithOne(e => e.Permission)
                .HasForeignKey(rp => rp.PermissionId)
                .IsRequired();
        });

        // Configure RolePermission join entity
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermissions");
            entity.HasKey(e => new { e.RoleId, e.PermissionId });
        });

        // Apply Core entity configurations
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        // modelBuilder.ApplyConfiguration(new SiteConfiguration());
        modelBuilder.ApplyConfiguration(new EmployeeConfiguration());
        // modelBuilder.ApplyConfiguration(new CompanyConfiguration());
        modelBuilder.ApplyConfiguration(new ContactConfiguration());

        // Apply Lookup entity configurations
        modelBuilder.ApplyConfiguration(new LookupCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new LookupValueConfiguration());
        modelBuilder.ApplyConfiguration(new TenantLookupValueConfiguration());

        // Apply Toolbox Talks entity configurations
        modelBuilder.ApplyConfiguration(new ToolboxTalkConfiguration());
        modelBuilder.ApplyConfiguration(new ToolboxTalkSectionConfiguration());
        modelBuilder.ApplyConfiguration(new ToolboxTalkQuestionConfiguration());
        modelBuilder.ApplyConfiguration(new ToolboxTalkScheduleConfiguration());
        modelBuilder.ApplyConfiguration(new ToolboxTalkScheduleAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new ScheduledTalkConfiguration());
        modelBuilder.ApplyConfiguration(new ScheduledTalkSectionProgressConfiguration());
        modelBuilder.ApplyConfiguration(new ScheduledTalkQuizAttemptConfiguration());
        modelBuilder.ApplyConfiguration(new ScheduledTalkCompletionConfiguration());
        modelBuilder.ApplyConfiguration(new ToolboxTalkSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new ToolboxTalkTranslationConfiguration());
        modelBuilder.ApplyConfiguration(new ToolboxTalkVideoTranslationConfiguration());
        modelBuilder.ApplyConfiguration(new ToolboxTalkCourseConfiguration());
        modelBuilder.ApplyConfiguration(new ToolboxTalkCourseItemConfiguration());
        modelBuilder.ApplyConfiguration(new ToolboxTalkCourseTranslationConfiguration());
        modelBuilder.ApplyConfiguration(new ToolboxTalkCourseAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new ToolboxTalkCertificateConfiguration());
        modelBuilder.ApplyConfiguration(new ToolboxTalkSlideConfiguration());
        modelBuilder.ApplyConfiguration(new ToolboxTalkSlideTranslationConfiguration());
        modelBuilder.ApplyConfiguration(new ToolboxTalkSlideshowTranslationConfiguration());
        modelBuilder.ApplyConfiguration(new SubtitleProcessingJobConfiguration());
        modelBuilder.ApplyConfiguration(new SubtitleTranslationConfiguration());

        // Apply global query filters - Core entities
        // BypassTenantFilter allows SuperUser to see all tenants' data when no tenant is selected
        modelBuilder.Entity<Site>().HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));
        modelBuilder.Entity<Employee>().HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));
        modelBuilder.Entity<Company>().HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));
        modelBuilder.Entity<Contact>().HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));

        // Note: Toolbox Talks query filters are defined in entity configurations
        // TenantEntity-based: ToolboxTalk, ToolboxTalkCourse, ToolboxTalkSchedule, ScheduledTalk, ToolboxTalkTranslation, ToolboxTalkVideoTranslation, ToolboxTalkCertificate, SubtitleProcessingJob, ToolboxTalkSlide
        // BaseEntity-based (not tenant-scoped): ToolboxTalkSlideshowTranslation
        // BaseEntity-based (not tenant-scoped): ToolboxTalkSection, ToolboxTalkQuestion, ToolboxTalkCourseItem, ToolboxTalkCourseTranslation,
        //   ToolboxTalkScheduleAssignment, ScheduledTalkSectionProgress, ScheduledTalkQuizAttempt, ScheduledTalkCompletion, ToolboxTalkSettings, SubtitleTranslation, ToolboxTalkSlideTranslation

        // Apply query filters for Lookup entities
        modelBuilder.Entity<LookupCategory>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<LookupValue>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TenantLookupValue>().HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));

        // Apply query filter for Permission (not tenant-scoped, global)
        modelBuilder.Entity<Permission>().HasQueryFilter(e => !e.IsDeleted);

        // Apply query filter for Tenant (not tenant-scoped, global)
        modelBuilder.Entity<Tenant>().HasQueryFilter(e => !e.IsDeleted);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Automatically set audit fields before saving
        SetAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        // Automatically set audit fields before saving
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

        // Handle User entity audit fields separately (doesn't inherit from BaseEntity)
        var userEntries = ChangeTracker.Entries<User>();
        foreach (var entry in userEntries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = CurrentUserId;
                    if (entry.Entity.TenantId == Guid.Empty)
                    {
                        entry.Entity.TenantId = TenantId;
                    }
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = CurrentUserId;
                    entry.Property(nameof(User.CreatedAt)).IsModified = false;
                    entry.Property(nameof(User.CreatedBy)).IsModified = false;
                    entry.Property(nameof(User.TenantId)).IsModified = false;
                    break;
            }
        }

        // Handle Role entity audit fields separately
        var roleEntries = ChangeTracker.Entries<Role>();
        foreach (var entry in roleEntries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = CurrentUserId;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = CurrentUserId;
                    entry.Property(nameof(Role.CreatedAt)).IsModified = false;
                    entry.Property(nameof(Role.CreatedBy)).IsModified = false;
                    break;
            }
        }
    }
}
