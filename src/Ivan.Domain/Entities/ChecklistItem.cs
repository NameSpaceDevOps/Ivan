using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class ChecklistItem : BaseTenantEntity
{
    public Guid ChecklistId { get; set; }
    public Guid? ParentItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool Done { get; set; } = false;
    public DateTimeOffset? DoneAt { get; set; }
    public Guid? DoneBy { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public Guid? AssigneePersonId { get; set; }
    public int SortOrder { get; set; } = 0;

    // Navigation
    public Checklist? Checklist { get; set; }
    public ChecklistItem? ParentItem { get; set; }
    public ICollection<ChecklistItem> SubItems { get; set; } = new List<ChecklistItem>();
}
