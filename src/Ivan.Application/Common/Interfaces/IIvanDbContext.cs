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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
