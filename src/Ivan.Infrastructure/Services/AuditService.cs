using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;

namespace Ivan.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;

    public AuditService(IIvanDbContext db, ICurrentUserContext userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    public async Task LogAsync(string action, string resourceType, string? resourceId, string summary, CancellationToken cancellationToken = default)
    {
        var log = new AuditLog
        {
            UserId = _userContext.UserId,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Summary = summary,
            At = DateTimeOffset.UtcNow
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
