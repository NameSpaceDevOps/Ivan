using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class MaintenancePlan : BaseTenantEntity
{
    public Guid AssetId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid RecurrenceId { get; set; }
    public DateTimeOffset? LastDoneAt { get; set; }
    public DateTimeOffset? NextDueAt { get; set; }
    public long? EstimatedCost { get; set; }

    // Navigation
    public Asset? Asset { get; set; }
    public Recurrence? Recurrence { get; set; }
}
