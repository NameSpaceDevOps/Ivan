using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class BalanceCheck : BaseTenantEntity
{
    public Guid AccountId { get; set; }
    public DateOnly Date { get; set; }
    public long StatementBalance { get; set; }
    public long Difference { get; set; }

    // Navigation
    public Account? Account { get; set; }
}
