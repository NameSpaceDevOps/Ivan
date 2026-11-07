using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class UtilityAccount : BaseTenantEntity
{
    public Guid PropertyId { get; set; }
    public UtilityKind Kind { get; set; } = UtilityKind.Electricity;
    public string Provider { get; set; } = string.Empty;
    public string AccountRef { get; set; } = string.Empty;
    public string? Notes { get; set; }

    // Navigation
    public Property? Property { get; set; }
    public ICollection<MeterReading> MeterReadings { get; set; } = new List<MeterReading>();
}
