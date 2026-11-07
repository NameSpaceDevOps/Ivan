using Ivan.Domain.Entities;
using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public record CreateGrantRequest(
    Guid GranteeUserId,
    string ResourceType,
    Guid? ResourceId,
    GrantPermission Permission,
    DateTimeOffset? ExpiresAt);

public interface IAccessGrantService
{
    Task<IReadOnlyList<AccessGrant>> GetActiveGrantsAsync(CancellationToken cancellationToken = default);
    Task<AccessGrant> GrantAccessAsync(CreateGrantRequest request, CancellationToken cancellationToken = default);
    Task<bool> RevokeGrantAsync(Guid grantId, CancellationToken cancellationToken = default);
    Task<bool> HasAccessAsync(Guid userId, string resourceType, Guid? resourceId, GrantPermission requiredPermission, CancellationToken cancellationToken = default);
}
