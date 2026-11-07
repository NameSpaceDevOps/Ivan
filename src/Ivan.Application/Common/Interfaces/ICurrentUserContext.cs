using Ivan.Domain.Enums;

namespace Ivan.Application.Common.Interfaces;

public interface ICurrentUserContext
{
    Guid? UserId { get; }
    Guid? HouseholdId { get; }
    MembershipRole? CurrentRole { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<Guid>? AllowedPropertyIds { get; }

    bool HasPropertyAccess(Guid propertyId);
    void SetContext(Guid? userId, Guid? householdId, MembershipRole? role, IReadOnlyList<Guid>? allowedPropertyIds = null);
}
