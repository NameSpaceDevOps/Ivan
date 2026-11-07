using Ivan.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Application.Common.Interfaces;

public interface IIvanDbContext
{
    DbSet<Household> Households { get; }
    DbSet<Membership> Memberships { get; }
    DbSet<AccessGrant> AccessGrants { get; }
    DbSet<Invite> Invites { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Property> Properties { get; }
    DbSet<Space> Spaces { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
