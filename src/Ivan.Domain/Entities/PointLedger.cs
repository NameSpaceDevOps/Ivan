using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class PointLedger : BaseTenantEntity
{
    public Guid PersonId { get; set; }
    public int Delta { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public Person? Person { get; set; }
}
