using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class MeterReading : BaseTenantEntity
{
    public Guid UtilityAccountId { get; set; }
    public DateOnly ReadingDate { get; set; }
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;

    // Navigation
    public UtilityAccount? UtilityAccount { get; set; }
}
