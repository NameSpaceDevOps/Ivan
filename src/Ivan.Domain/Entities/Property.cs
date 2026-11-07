using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class Property : BaseTenantEntity
{
    public string Name { get; set; } = string.Empty;
    public PropertyType Type { get; set; } = PropertyType.Home;
    public string? Address { get; set; }
    public string? GeoJson { get; set; }
    public string? Notes { get; set; }
    public string? ExtraJson { get; set; }

    // Navigation
    public Household? Household { get; set; }
    public ICollection<Space> Spaces { get; set; } = new List<Space>();
}
