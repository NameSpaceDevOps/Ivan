using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class Checklist : BaseTenantEntity
{
    public string Title { get; set; } = string.Empty;
    public string? OwnerType { get; set; } // e.g. "Property", "Routine", "General"
    public Guid? OwnerId { get; set; }
    public Guid? TemplateId { get; set; }
    public bool IsTemplate { get; set; } = false;
    public Guid? RecurrenceId { get; set; }
    public ResourceVisibility Visibility { get; set; } = ResourceVisibility.Household;

    // Navigation
    public Recurrence? Recurrence { get; set; }
    public ICollection<ChecklistItem> Items { get; set; } = new List<ChecklistItem>();

    public int TotalCount => Items.Count;
    public int DoneCount => Items.Count(i => i.Done);
    public double ProgressPercentage => TotalCount == 0 ? 0 : Math.Round((double)DoneCount / TotalCount * 100, 1);
}
