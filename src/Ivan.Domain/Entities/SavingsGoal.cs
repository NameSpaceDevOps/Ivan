using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class SavingsGoal : BaseTenantEntity
{
    public Guid PersonId { get; set; }
    public Guid WalletAccountId { get; set; }
    public string Title { get; set; } = string.Empty;
    public long TargetAmount { get; set; }
    public DateOnly? TargetDate { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.InProgress;

    // Navigation
    public Person? Person { get; set; }
    public Account? WalletAccount { get; set; }
}
