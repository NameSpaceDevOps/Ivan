using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class AuditLog : BaseTenantEntity
{
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty; // e.g. "Create", "Read", "Update", "Delete", "Grant"
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public string? Ip { get; set; }
}
