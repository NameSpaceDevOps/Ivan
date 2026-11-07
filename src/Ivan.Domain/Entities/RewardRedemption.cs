using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class RewardRedemption : BaseTenantEntity
{
    public Guid RewardId { get; set; }
    public Guid PersonId { get; set; }
    public RedemptionStatus Status { get; set; } = RedemptionStatus.Requested;
    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }

    // Navigation
    public Reward? Reward { get; set; }
    public Person? Person { get; set; }
}
