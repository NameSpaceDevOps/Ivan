using Ivan.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Application.Common.Interfaces;

public interface IIvanDbContext
{
    DbSet<Household> Households { get; }
    DbSet<Membership> Memberships { get; }
    DbSet<AccessGrant> AccessGrants { get; }
    DbSet<Invite> Invites { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Property> Properties { get; }
    DbSet<Space> Spaces { get; }

    // Phase 2 Engine
    DbSet<Recurrence> Recurrences { get; }
    DbSet<Checklist> Checklists { get; }
    DbSet<ChecklistItem> ChecklistItems { get; }
    DbSet<TaskItem> TaskItems { get; }
    DbSet<Reminder> Reminders { get; }
    DbSet<NotificationChannel> NotificationChannels { get; }
    DbSet<NotificationLog> NotificationLogs { get; }
    DbSet<Attachment> Attachments { get; }
    DbSet<Tag> Tags { get; }
    DbSet<TagLink> TagLinks { get; }

    // Phase 3 Home, Assets, People
    DbSet<UtilityAccount> UtilityAccounts { get; }
    DbSet<MeterReading> MeterReadings { get; }
    DbSet<Contact> Contacts { get; }
    DbSet<AssetCategory> AssetCategories { get; }
    DbSet<Asset> Assets { get; }
    DbSet<MaintenancePlan> MaintenancePlans { get; }
    DbSet<ServiceRecord> ServiceRecords { get; }
    DbSet<Person> People { get; }
    DbSet<PersonGuardian> PersonGuardians { get; }
    DbSet<HealthRecord> HealthRecords { get; }
    DbSet<EducationRecord> EducationRecords { get; }
    DbSet<PersonDocument> PersonDocuments { get; }
    DbSet<ImportantDate> ImportantDates { get; }
    DbSet<TimelineEvent> TimelineEvents { get; }

    // Phase 4 Finance
    DbSet<Account> Accounts { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalLine> JournalLines { get; }
    DbSet<Budget> Budgets { get; }
    DbSet<RecurringTransaction> RecurringTransactions { get; }
    DbSet<Cheque> Cheques { get; }
    DbSet<Loan> Loans { get; }
    DbSet<Installment> Installments { get; }
    DbSet<BalanceCheck> BalanceChecks { get; }

    // Phase 5 Children, Wallet, Homework
    DbSet<RoutineTask> RoutineTasks { get; }
    DbSet<RoutineCompletion> RoutineCompletions { get; }
    DbSet<PointLedger> PointLedgers { get; }
    DbSet<Reward> Rewards { get; }
    DbSet<RewardRedemption> RewardRedemptions { get; }
    DbSet<Subject> Subjects { get; }
    DbSet<HomeworkItem> HomeworkItems { get; }
    DbSet<ExamEvent> ExamEvents { get; }
    DbSet<SchoolSchedule> SchoolSchedules { get; }
    DbSet<WalletLink> WalletLinks { get; }
    DbSet<AllowanceRule> AllowanceRules { get; }
    DbSet<SavingsGoal> SavingsGoals { get; }
    DbSet<WalletRequest> WalletRequests { get; }

    // Phase 6 Library and Media
    DbSet<Book> Books { get; }
    DbSet<BookReadProgress> BookReadProgresses { get; }
    DbSet<BookLoan> BookLoans { get; }
    DbSet<MediaItem> MediaItems { get; }
    DbSet<MediaPersonProgress> MediaPersonProgresses { get; }
    DbSet<MediaEpisode> MediaEpisodes { get; }
    DbSet<EpisodeWatchProgress> EpisodeWatchProgresses { get; }

    // Phase 7 Dashboard, Backup, AI
    DbSet<HouseholdAiConfig> HouseholdAiConfigs { get; }
    DbSet<AiUsageLog> AiUsageLogs { get; }
    DbSet<BackupRecord> BackupRecords { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
