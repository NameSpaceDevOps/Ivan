using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class Membership : BaseTenantEntity
{
    public Guid UserId { get; set; }
    public MembershipRole Role { get; set; } = MembershipRole.Parent;
    
    // null means all properties; or JSON array of Property IDs
    public string? PropertyScopeJson { get; set; }

    // Bitmask or comma-separated list of granted modules
    public string? ModuleMask { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public Household? Household { get; set; }
}
