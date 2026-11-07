using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class ImportantDate : BaseTenantEntity
{
    public Guid? PersonId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public bool RepeatsYearly { get; set; } = true;
    public string ReminderOffsets { get; set; } = "0,1,7";

    // Navigation
    public Person? Person { get; set; }
}
