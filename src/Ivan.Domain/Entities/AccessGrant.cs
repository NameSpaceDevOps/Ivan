using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class AccessGrant : BaseTenantEntity
{
    public Guid GranteeUserId { get; set; }
    
    // Resource category, e.g. "Property", "Document", "Finance", "Health"
    public string ResourceType { get; set; } = string.Empty;

    // null means all resources of that type; or specific entity ID
    public Guid? ResourceId { get; set; }

    public GrantPermission Permission { get; set; } = GrantPermission.View;
    public DateTimeOffset? ExpiresAt { get; set; }
    public Guid GrantedBy { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsEffective => RevokedAt == null && (!ExpiresAt.HasValue || ExpiresAt.Value > DateTimeOffset.UtcNow);
}
