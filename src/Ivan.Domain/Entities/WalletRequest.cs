using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class WalletRequest : BaseTenantEntity
{
    public Guid PersonId { get; set; }
    public WalletRequestKind Kind { get; set; } = WalletRequestKind.Purchase;
    public long Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public WalletRequestStatus Status { get; set; } = WalletRequestStatus.Pending;
    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }

    // Navigation
    public Person? Person { get; set; }
}
