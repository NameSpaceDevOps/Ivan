using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class AssetCategory : BaseTenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "inventory_2";

    // Navigation
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
