using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class RecurringTransaction : BaseTenantEntity
{
    public string TemplateJson { get; set; } = string.Empty;
    public Guid RecurrenceId { get; set; }
    public bool AutoPost { get; set; } = false;

    // Navigation
    public Recurrence? Recurrence { get; set; }
}
