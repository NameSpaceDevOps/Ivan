using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class AccessGrantService : IAccessGrantService
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;

    public AccessGrantService(IIvanDbContext db, ICurrentUserContext userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    public async Task<IReadOnlyList<AccessGrant>> GetActiveGrantsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _db.AccessGrants
            .Where(g => g.RevokedAt == null && (!g.ExpiresAt.HasValue || g.ExpiresAt.Value > now))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<AccessGrant> GrantAccessAsync(CreateGrantRequest request, CancellationToken cancellationToken = default)
    {
        var grant = new AccessGrant
        {
            GranteeUserId = request.GranteeUserId,
            ResourceType = request.ResourceType,
            ResourceId = request.ResourceId,
            Permission = request.Permission,
            ExpiresAt = request.ExpiresAt,
            GrantedBy = _userContext.UserId ?? Guid.Empty
        };

        _db.AccessGrants.Add(grant);
        await _db.SaveChangesAsync(cancellationToken);
        return grant;
    }

    public async Task<bool> RevokeGrantAsync(Guid grantId, CancellationToken cancellationToken = default)
    {
        var grant = await _db.AccessGrants.FirstOrDefaultAsync(g => g.Id == grantId, cancellationToken);
        if (grant == null) return false;

        grant.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> HasAccessAsync(Guid userId, string resourceType, Guid? resourceId, GrantPermission requiredPermission, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var grants = await _db.AccessGrants
            .Where(g => g.GranteeUserId == userId &&
                        g.ResourceType == resourceType &&
                        g.RevokedAt == null &&
                        (!g.ExpiresAt.HasValue || g.ExpiresAt.Value > now))
            .ToListAsync(cancellationToken);

        // Check if there is an all-of-type grant or specific resource grant matching required permission
        return grants.Any(g =>
            (g.ResourceId == null || g.ResourceId == resourceId) &&
            g.Permission >= requiredPermission);
    }
}
