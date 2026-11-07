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

    // Phase 2 Engine
    public DbSet<Recurrence> Recurrences => Set<Recurrence>();
    public DbSet<Checklist> Checklists => Set<Checklist>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<NotificationChannel> NotificationChannels => Set<NotificationChannel>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TagLink> TagLinks => Set<TagLink>();

    // Phase 3 Home, Assets, People
    public DbSet<UtilityAccount> UtilityAccounts => Set<UtilityAccount>();
    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<MaintenancePlan> MaintenancePlans => Set<MaintenancePlan>();
    public DbSet<ServiceRecord> ServiceRecords => Set<ServiceRecord>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<PersonGuardian> PersonGuardians => Set<PersonGuardian>();
    public DbSet<HealthRecord> HealthRecords => Set<HealthRecord>();
    public DbSet<EducationRecord> EducationRecords => Set<EducationRecord>();
    public DbSet<PersonDocument> PersonDocuments => Set<PersonDocument>();
    public DbSet<ImportantDate> ImportantDates => Set<ImportantDate>();
    public DbSet<TimelineEvent> TimelineEvents => Set<TimelineEvent>();

    // Phase 4 Finance
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();
    public DbSet<Cheque> Cheques => Set<Cheque>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<Installment> Installments => Set<Installment>();
    public DbSet<BalanceCheck> BalanceChecks => Set<BalanceCheck>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Table names
        builder.Entity<Household>().ToTable("Households");
        builder.Entity<Membership>().ToTable("Memberships");
        builder.Entity<AccessGrant>().ToTable("AccessGrants");
        builder.Entity<Invite>().ToTable("Invites");
        builder.Entity<AuditLog>().ToTable("AuditLogs");
        builder.Entity<Property>().ToTable("Properties");
        builder.Entity<Space>().ToTable("Spaces");

        builder.Entity<Recurrence>().ToTable("Recurrences");
        builder.Entity<Checklist>().ToTable("Checklists");
        builder.Entity<ChecklistItem>().ToTable("ChecklistItems");
        builder.Entity<TaskItem>().ToTable("TaskItems");
        builder.Entity<Reminder>().ToTable("Reminders");
        builder.Entity<NotificationChannel>().ToTable("NotificationChannels");
        builder.Entity<NotificationLog>().ToTable("NotificationLogs");
        builder.Entity<Attachment>().ToTable("Attachments");
        builder.Entity<Tag>().ToTable("Tags");
        builder.Entity<TagLink>().ToTable("TagLinks");

        // Phase 3 Tables
        builder.Entity<UtilityAccount>().ToTable("UtilityAccounts");
        builder.Entity<MeterReading>().ToTable("MeterReadings");
        builder.Entity<Contact>().ToTable("Contacts");
        builder.Entity<AssetCategory>().ToTable("AssetCategories");
        builder.Entity<Asset>().ToTable("Assets");
        builder.Entity<MaintenancePlan>().ToTable("MaintenancePlans");
        builder.Entity<ServiceRecord>().ToTable("ServiceRecords");
        builder.Entity<Person>().ToTable("People");
        builder.Entity<PersonGuardian>().ToTable("PersonGuardians");
        builder.Entity<HealthRecord>().ToTable("HealthRecords");
        builder.Entity<EducationRecord>().ToTable("EducationRecords");
        builder.Entity<PersonDocument>().ToTable("PersonDocuments");
        builder.Entity<ImportantDate>().ToTable("ImportantDates");
        builder.Entity<TimelineEvent>().ToTable("TimelineEvents");

        // Phase 4 Tables
        builder.Entity<Account>().ToTable("Accounts");
        builder.Entity<JournalEntry>().ToTable("JournalEntries");
        builder.Entity<JournalLine>().ToTable("JournalLines");
        builder.Entity<Budget>().ToTable("Budgets");
        builder.Entity<RecurringTransaction>().ToTable("RecurringTransactions");
        builder.Entity<Cheque>().ToTable("Cheques");
        builder.Entity<Loan>().ToTable("Loans");
        builder.Entity<Installment>().ToTable("Installments");
        builder.Entity<BalanceCheck>().ToTable("BalanceChecks");

        // Relationships
        builder.Entity<Account>()
            .HasOne(a => a.ParentAccount)
            .WithMany(a => a.ChildAccounts)
            .HasForeignKey(a => a.ParentAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<JournalEntry>()
            .HasOne(je => je.ReversalOf)
            .WithMany()
            .HasForeignKey(je => je.ReversalOfId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<JournalLine>()
            .HasOne(jl => jl.Entry)
            .WithMany(je => je.Lines)
            .HasForeignKey(jl => jl.EntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<JournalLine>()
            .HasOne(jl => jl.Account)
            .WithMany(a => a.JournalLines)
            .HasForeignKey(jl => jl.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Installment>()
            .HasOne(i => i.Loan)
            .WithMany(l => l.Installments)
            .HasForeignKey(i => i.LoanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PersonGuardian>()
            .HasOne(pg => pg.Person)
            .WithMany(p => p.Guardians)
            .HasForeignKey(pg => pg.PersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PersonGuardian>()
            .HasOne(pg => pg.GuardianPerson)
            .WithMany(p => p.WardOf)
            .HasForeignKey(pg => pg.GuardianPersonId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.Entity<Membership>().HasIndex(m => new { m.HouseholdId, m.UserId }).IsUnique();
        builder.Entity<Property>().HasIndex(p => new { p.HouseholdId, p.Name });
        builder.Entity<Space>().HasIndex(s => new { s.HouseholdId, s.PropertyId });
        builder.Entity<AuditLog>().HasIndex(a => new { a.HouseholdId, a.At });

        builder.Entity<Checklist>().HasIndex(c => new { c.HouseholdId, c.IsTemplate });
        builder.Entity<ChecklistItem>().HasIndex(ci => new { ci.HouseholdId, ci.ChecklistId });
        builder.Entity<TaskItem>().HasIndex(t => new { t.HouseholdId, t.Status });
        builder.Entity<Reminder>().HasIndex(r => new { r.HouseholdId, r.DedupKey }).IsUnique();
        builder.Entity<Reminder>().HasIndex(r => new { r.State, r.FireAt });
        builder.Entity<Attachment>().HasIndex(a => new { a.HouseholdId, a.Sha256 });
        builder.Entity<TagLink>().HasIndex(tl => new { tl.HouseholdId, tl.TargetType, tl.TargetId });

        // Phase 3 Indexes
        builder.Entity<UtilityAccount>().HasIndex(u => new { u.HouseholdId, u.PropertyId });
        builder.Entity<MeterReading>().HasIndex(m => new { m.HouseholdId, m.UtilityAccountId });
        builder.Entity<Contact>().HasIndex(c => new { c.HouseholdId, c.Name });
        builder.Entity<AssetCategory>().HasIndex(ac => new { ac.HouseholdId, ac.Name });
        builder.Entity<Asset>().HasIndex(a => new { a.HouseholdId, a.PropertyId });
        builder.Entity<MaintenancePlan>().HasIndex(mp => new { mp.HouseholdId, mp.AssetId });
        builder.Entity<ServiceRecord>().HasIndex(sr => new { sr.HouseholdId, sr.AssetId });
        builder.Entity<Person>().HasIndex(p => new { p.HouseholdId, p.DisplayName });
        builder.Entity<PersonGuardian>().HasIndex(pg => new { pg.HouseholdId, pg.PersonId, pg.GuardianPersonId });
        builder.Entity<HealthRecord>().HasIndex(hr => new { hr.HouseholdId, hr.PersonId });
        builder.Entity<EducationRecord>().HasIndex(er => new { er.HouseholdId, er.PersonId });
        builder.Entity<PersonDocument>().HasIndex(pd => new { pd.HouseholdId, pd.PersonId });
        builder.Entity<ImportantDate>().HasIndex(id => new { id.HouseholdId, id.Date });
        builder.Entity<TimelineEvent>().HasIndex(te => new { te.HouseholdId, te.SubjectType, te.SubjectId });

        // Phase 4 Indexes
        builder.Entity<Account>().HasIndex(a => new { a.HouseholdId, a.Code });
        builder.Entity<Account>().HasIndex(a => new { a.HouseholdId, a.Type });
        builder.Entity<Account>().HasIndex(a => new { a.HouseholdId, a.ParentAccountId });
        builder.Entity<JournalEntry>().HasIndex(je => new { je.HouseholdId, je.Date });
        builder.Entity<JournalEntry>().HasIndex(je => new { je.HouseholdId, je.Status });
        builder.Entity<JournalEntry>().HasIndex(je => new { je.HouseholdId, je.SourceType, je.SourceId });
        builder.Entity<JournalLine>().HasIndex(jl => new { jl.HouseholdId, jl.EntryId });
        builder.Entity<JournalLine>().HasIndex(jl => new { jl.HouseholdId, jl.AccountId });
        builder.Entity<JournalLine>().HasIndex(jl => new { jl.HouseholdId, jl.PropertyId });
        builder.Entity<JournalLine>().HasIndex(jl => new { jl.HouseholdId, jl.PersonId });
        builder.Entity<JournalLine>().HasIndex(jl => new { jl.HouseholdId, jl.AssetId });
        builder.Entity<Budget>().HasIndex(b => new { b.HouseholdId, b.CategoryAccountId, b.StartsOn });
        builder.Entity<Cheque>().HasIndex(c => new { c.HouseholdId, c.DueDate });
        builder.Entity<Cheque>().HasIndex(c => new { c.HouseholdId, c.Status });
        builder.Entity<Loan>().HasIndex(l => new { l.HouseholdId, l.StartDate });
        builder.Entity<Installment>().HasIndex(i => new { i.HouseholdId, i.LoanId, i.DueDate });
        builder.Entity<BalanceCheck>().HasIndex(bc => new { bc.HouseholdId, bc.AccountId, bc.Date });

        // AppUser & Household soft delete filters
        builder.Entity<AppUser>().HasQueryFilter(u => u.DeletedAt == null);
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
            // Soft delete interception
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
