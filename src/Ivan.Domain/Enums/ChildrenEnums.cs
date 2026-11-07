namespace Ivan.Domain.Enums;

public enum RoutineTimeSlot
{
    Morning = 1,
    Afternoon = 2,
    Evening = 3,
    Bedtime = 4,
    Anytime = 5
}

public enum CompletionStatus
{
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3
}

public enum RedemptionStatus
{
    Requested = 1,
    Approved = 2,
    Rejected = 3
}

public enum HomeworkStatus
{
    Assigned = 1,
    Completed = 2,
    Reviewed = 3
}

public enum WalletMode
{
    Virtual = 1,
    BankLinked = 2
}

public enum GoalStatus
{
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}

public enum WalletRequestKind
{
    Withdrawal = 1,
    Purchase = 2
}

public enum WalletRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}
