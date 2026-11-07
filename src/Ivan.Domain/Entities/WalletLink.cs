using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class WalletLink : BaseTenantEntity
{
    public Guid WalletAccountId { get; set; }
    public Guid PersonId { get; set; }
    public WalletMode Mode { get; set; } = WalletMode.Virtual;
    public Guid? BankAccountId { get; set; }
    public Guid? CustodianPersonId { get; set; }

    // Navigation
    public Account? WalletAccount { get; set; }
    public Person? Person { get; set; }
    public Account? BankAccount { get; set; }
    public Person? CustodianPerson { get; set; }
}
