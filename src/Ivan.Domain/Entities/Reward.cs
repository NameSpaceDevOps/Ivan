using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class Reward : BaseTenantEntity
{
    public string Title { get; set; } = string.Empty;
    public int Cost { get; set; }
    public string Icon { get; set; } = "card_giftcard";
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<RewardRedemption> Redemptions { get; set; } = new List<RewardRedemption>();
}
