using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class HealthRecord : BaseTenantEntity
{
    public Guid PersonId { get; set; }
    public HealthRecordType Type { get; set; } = HealthRecordType.Condition;
    public string Title { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public DateOnly? NextDueDate { get; set; }
    public string? DetailsJson { get; set; }
    public ResourceVisibility Visibility { get; set; } = ResourceVisibility.Guardians;

    // Navigation
    public Person? Person { get; set; }
}
