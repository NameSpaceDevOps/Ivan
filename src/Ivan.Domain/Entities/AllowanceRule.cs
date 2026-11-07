using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class AllowanceRule : BaseTenantEntity
{
    public Guid PersonId { get; set; }
    public long Amount { get; set; }
    public Guid? RecurrenceId { get; set; }
    public Guid FromAccountId { get; set; }
    public Guid WalletAccountId { get; set; }
    public bool Active { get; set; } = true;

    // Navigation
    public Person? Person { get; set; }
    public Recurrence? Recurrence { get; set; }
    public Account? FromAccount { get; set; }
    public Account? WalletAccount { get; set; }
}
