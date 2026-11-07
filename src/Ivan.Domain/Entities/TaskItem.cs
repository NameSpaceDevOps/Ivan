using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class TaskItem : BaseTenantEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Details { get; set; }
    public Guid? AssigneePersonId { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public Guid? RecurrenceId { get; set; }
    public Guid? PropertyId { get; set; }
    public string? LinkType { get; set; } // e.g. "Asset", "Vehicle", "School"
    public Guid? LinkId { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    // Navigation
    public Recurrence? Recurrence { get; set; }
    public Property? Property { get; set; }
}
