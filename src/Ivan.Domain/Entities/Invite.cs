using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class Invite : BaseTenantEntity
{
    public string TokenHash { get; set; } = string.Empty;
    public MembershipRole RolePreset { get; set; } = MembershipRole.Parent;
    public string? PropertyScopeJson { get; set; }
    public string? ModuleMask { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }

    public bool IsValid => UsedAt == null && ExpiresAt > DateTimeOffset.UtcNow;
}
