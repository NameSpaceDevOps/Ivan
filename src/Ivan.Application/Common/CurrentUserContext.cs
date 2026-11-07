using Ivan.Application.Common.Interfaces;
using Ivan.Domain.Enums;

namespace Ivan.Application.Common;

public class CurrentUserContext : ICurrentUserContext
{
    public Guid? UserId { get; private set; }
    public Guid? HouseholdId { get; private set; }
    public MembershipRole? CurrentRole { get; private set; }
    public bool IsAuthenticated => UserId.HasValue;
    public IReadOnlyList<Guid>? AllowedPropertyIds { get; private set; }

    public bool HasPropertyAccess(Guid propertyId)
    {
        // If AllowedPropertyIds is null, user has full scope access across household
        if (AllowedPropertyIds == null)
            return true;

        return AllowedPropertyIds.Contains(propertyId);
    }

    public void SetContext(Guid? userId, Guid? householdId, MembershipRole? role, IReadOnlyList<Guid>? allowedPropertyIds = null)
    {
        UserId = userId;
        HouseholdId = householdId;
        CurrentRole = role;
        AllowedPropertyIds = allowedPropertyIds;
    }
}
