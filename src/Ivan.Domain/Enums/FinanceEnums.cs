namespace Ivan.Domain.Enums;

public enum AccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Income = 4,
    Expense = 5
}

public enum AccountSubType
{
    Cash = 1,
    Bank = 2,
    Card = 3,
    Wallet = 4,
    Receivable = 5,
    Payable = 6,
    Loan = 7,
    Category = 8,
    System = 9
}

public enum JournalEntryStatus
{
    Posted = 1,
    Voided = 2
}

public enum BudgetPeriod
{
    Monthly = 1,
    Yearly = 2
}

public enum ChequeDirection
{
    Issued = 1,
    Received = 2
}

public enum ChequeStatus
{
    Pending = 1,
    Deposited = 2,
    Cleared = 3,
    Bounced = 4,
    Returned = 5
}

public enum LoanDirection
{
    Borrowed = 1,
    Lent = 2
}

public enum InstallmentStatus
{
    Pending = 1,
    Paid = 2,
    Overdue = 3
}

public enum ChartPreset
{
    Full = 1,
    Compact = 2,
    Empty = 3
}
