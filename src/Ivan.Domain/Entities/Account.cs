using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class Account : BaseTenantEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PersianName { get; set; } = string.Empty;
    public AccountType Type { get; set; } = AccountType.Expense;
    public AccountSubType SubType { get; set; } = AccountSubType.Category;
    public string Currency { get; set; } = "IRR";
    public Guid? ParentAccountId { get; set; }
    public Guid? OwnerPersonId { get; set; }
    public ResourceVisibility Visibility { get; set; } = ResourceVisibility.Household;
    public string? BankInfo { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSystem { get; set; } = false;
    public bool IsGroup { get; set; } = false;

    // Navigation
    public Account? ParentAccount { get; set; }
    public ICollection<Account> ChildAccounts { get; set; } = new List<Account>();
    public Person? OwnerPerson { get; set; }
    public ICollection<JournalLine> JournalLines { get; set; } = new List<JournalLine>();
}
