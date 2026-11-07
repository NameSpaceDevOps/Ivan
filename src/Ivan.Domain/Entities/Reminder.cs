using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class Reminder : BaseTenantEntity
{
    public string SubjectType { get; set; } = string.Empty; // e.g. "Task", "Checklist", "Cheque", "Warranty"
    public Guid SubjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public DateTimeOffset FireAt { get; set; }
    public int OffsetMinutes { get; set; } = 0;
    public string? RecipientUserIdsJson { get; set; } // JSON array of user IDs
    public ReminderState State { get; set; } = ReminderState.Pending;
    public int Attempts { get; set; } = 0;
    public string? LastError { get; set; }
    public string DedupKey { get; set; } = string.Empty; // Idempotency key (Section 3.5)
}
