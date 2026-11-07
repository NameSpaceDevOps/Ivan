using System.Reflection;
using Ivan.Application.Common.Interfaces;
using Ivan.Domain.Common;
using Ivan.Domain.Entities;
using Ivan.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Data;

public class IvanDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>, IIvanDbContext
{
    private readonly ICurrentUserContext _currentUserContext;

    public Guid? CurrentHouseholdId => _currentUserContext.HouseholdId;

    public IvanDbContext(
        DbContextOptions<IvanDbContext> options,
        ICurrentUserContext currentUserContext)
        : base(options)
    {
        _currentUserContext = currentUserContext;
    }

    public DbSet<Household> Households => Set<Household>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<AccessGrant> AccessGrants => Set<AccessGrant>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Space> Spaces => Set<Space>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure table names
        builder.Entity<Household>().ToTable("Households");
        builder.Entity<Membership>().ToTable("Memberships");
        builder.Entity<AccessGrant>().ToTable("AccessGrants");
        builder.Entity<Invite>().ToTable("Invites");
        builder.Entity<AuditLog>().ToTable("AuditLogs");
        builder.Entity<Property>().ToTable("Properties");
        builder.Entity<Space>().ToTable("Spaces");

        // Indexes
        builder.Entity<Membership>().HasIndex(m => new { m.HouseholdId, m.UserId }).IsUnique();
        builder.Entity<Property>().HasIndex(p => new { p.HouseholdId, p.Name });
        builder.Entity<Space>().HasIndex(s => new { s.HouseholdId, s.PropertyId });
        builder.Entity<AuditLog>().HasIndex(a => new { a.HouseholdId, a.At });

        // AppUser soft delete
        builder.Entity<AppUser>().HasQueryFilter(u => u.DeletedAt == null);

        // Household soft delete
        builder.Entity<Household>().HasQueryFilter(h => h.DeletedAt == null);

        // Apply tenant and soft delete filters to all BaseTenantEntity types
        var tenantFilterMethod = typeof(IvanDbContext)
            .GetMethod(nameof(ConfigureTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(BaseTenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var genericMethod = tenantFilterMethod.MakeGenericMethod(entityType.ClrType);
                genericMethod.Invoke(this, [builder]);
            }
        }
    }

    private void ConfigureTenantFilter<T>(ModelBuilder builder) where T : BaseTenantEntity
    {
        builder.Entity<T>().HasQueryFilter(e =>
            e.DeletedAt == null &&
            (CurrentHouseholdId == null || e.HouseholdId == CurrentHouseholdId));
    }

    public override int SaveChanges()
    {
        StampAndApplySoftDelete();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAndApplySoftDelete();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampAndApplySoftDelete()
    {
        var now = DateTimeOffset.UtcNow;
        var currentUserId = _currentUserContext.UserId;
        var currentHouseholdId = _currentUserContext.HouseholdId;

        foreach (var entry in ChangeTracker.Entries())
        {
            // Soft delete interception (Story 1.8)
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable softDeletable)
            {
                entry.State = EntityState.Modified;
                softDeletable.DeletedAt = now;
            }

            // Stamping on Added entities
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity is IAuditable auditable)
                {
                    auditable.CreatedAt = now;
                    auditable.CreatedBy ??= currentUserId;
                }

                // Automatic stamping of HouseholdId (Section 3.2 / Story 1.3)
                if (entry.Entity is ITenantEntity tenant && tenant.HouseholdId == Guid.Empty && currentHouseholdId.HasValue)
                {
                    tenant.HouseholdId = currentHouseholdId.Value;
                }
            }

            // Stamping on Modified entities
            if (entry.State == EntityState.Modified)
            {
                if (entry.Entity is IAuditable auditable)
                {
                    auditable.UpdatedAt = now;
                    auditable.UpdatedBy = currentUserId;
                }
            }
        }
    }
}
