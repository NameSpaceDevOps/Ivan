using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class Asset : BaseTenantEntity
{
    public Guid? PropertyId { get; set; }
    public Guid? SpaceId { get; set; }
    public Guid? CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNo { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public long? PurchasePrice { get; set; }
    public string? Vendor { get; set; }
    public DateOnly? WarrantyEnd { get; set; }
    public AssetStatus Status { get; set; } = AssetStatus.InUse;
    public string? ExtraJson { get; set; }

    // Navigation
    public Property? Property { get; set; }
    public Space? Space { get; set; }
    public AssetCategory? Category { get; set; }
    public ICollection<MaintenancePlan> MaintenancePlans { get; set; } = new List<MaintenancePlan>();
    public ICollection<ServiceRecord> ServiceRecords { get; set; } = new List<ServiceRecord>();
}
