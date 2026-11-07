using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class Budget : BaseTenantEntity
{
    public Guid CategoryAccountId { get; set; }
    public BudgetPeriod Period { get; set; } = BudgetPeriod.Monthly;
    public long Amount { get; set; }
    public Guid? PropertyId { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }

    // Navigation
    public Account? CategoryAccount { get; set; }
    public Property? Property { get; set; }
}
