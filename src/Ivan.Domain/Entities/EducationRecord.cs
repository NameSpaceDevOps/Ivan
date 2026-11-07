using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class EducationRecord : BaseTenantEntity
{
    public Guid PersonId { get; set; }
    public string Institution { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public int StartYear { get; set; }
    public int? EndYear { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public Person? Person { get; set; }
}
