using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public record AccountDto(
    Guid Id,
    string Code,
    string Name,
    string PersianName,
    AccountType Type,
    AccountSubType SubType,
    string Currency,
    Guid? ParentAccountId,
    Guid? OwnerPersonId,
    ResourceVisibility Visibility,
    string? BankInfo,
    bool IsActive,
    bool IsSystem,
    bool IsGroup,
    long CurrentBalance);

public record CreateAccountRequest(
    string Code,
    string Name,
    string PersianName,
    AccountType Type,
    AccountSubType SubType,
    string Currency = "IRR",
    Guid? ParentAccountId = null,
    Guid? OwnerPersonId = null,
    ResourceVisibility Visibility = ResourceVisibility.Household,
    string? BankInfo = null,
    bool IsGroup = false);

public record RecordExpenseRequest(
    DateOnly Date,
    long Amount,
    Guid ExpenseAccountId,
    Guid PaymentAccountId,
    string Description,
    Guid? PropertyId = null,
    Guid? PersonId = null,
    Guid? AssetId = null,
    string? Reference = null);

public record RecordIncomeRequest(
    DateOnly Date,
    long Amount,
    Guid IncomeAccountId,
    Guid DestinationAccountId,
    string Description,
    Guid? PersonId = null,
    string? Reference = null);

public record RecordTransferRequest(
    DateOnly Date,
    long Amount,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    string Description,
    string? Reference = null);

public record MonthlySummaryReport(
    int Year,
    int Month,
    long TotalIncome,
    long TotalExpense,
    long NetSavings,
    double SavingsRatePercent);

public record CategoryBreakdownItem(
    Guid AccountId,
    string Code,
    string Name,
    long Amount,
    double Percentage);

public record NetWorthReport(
    long TotalAssets,
    long TotalLiabilities,
    long NetWorth);

public record DimensionalCostReport(
    Guid? PropertyId,
    Guid? AssetId,
    long TotalCost,
    int TransactionCount);

public record BudgetVsActualItem(
    Guid AccountId,
    string AccountName,
    long BudgetAmount,
    long ActualAmount,
    long Variance,
    double UtilizationPercent);

public record SetBudgetRequest(
    Guid CategoryAccountId,
    BudgetPeriod Period,
    long Amount,
    DateOnly StartsOn,
    DateOnly? EndsOn = null,
    Guid? PropertyId = null);

public record ChequeDto(
    Guid Id,
    ChequeDirection Direction,
    string Number,
    string? SayadId,
    string Bank,
    long Amount,
    string Currency,
    DateOnly DueDate,
    Guid? CounterpartyContactId,
    string? CounterpartyName,
    ChequeStatus Status,
    Guid? JournalEntryId);

public record CreateChequeRequest(
    ChequeDirection Direction,
    string Number,
    string Bank,
    long Amount,
    DateOnly DueDate,
    string? SayadId = null,
    string Currency = "IRR",
    Guid? CounterpartyContactId = null);

public record LoanDto(
    Guid Id,
    LoanDirection Direction,
    long Principal,
    string Currency,
    DateOnly StartDate,
    decimal? InterestRate,
    bool InterestFree,
    int InstallmentCount,
    int PaidInstallmentCount,
    long RemainingBalance,
    Guid? CounterpartyContactId,
    string? CounterpartyName,
    IReadOnlyList<InstallmentDto> Installments);

public record InstallmentDto(
    Guid Id,
    Guid LoanId,
    int No,
    DateOnly DueDate,
    long Amount,
    long PaidAmount,
    DateTimeOffset? PaidAt,
    Guid? JournalEntryId,
    InstallmentStatus Status);

public record CreateLoanRequest(
    LoanDirection Direction,
    long Principal,
    DateOnly StartDate,
    int InstallmentCount,
    long InstallmentAmount,
    Guid CounterpartyContactId,
    bool InterestFree = true,
    decimal? InterestRate = null,
    string Currency = "IRR");

public record BalanceCheckDto(
    Guid Id,
    Guid AccountId,
    DateOnly Date,
    long StatementBalance,
    long LedgerBalance,
    long Difference);

public interface IFinanceService
{
    // Accounts
    Task<IReadOnlyList<AccountDto>> GetAccountsAsync(AccountType? type = null, bool includeInactive = false, CancellationToken ct = default);
    Task<AccountDto> CreateAccountAsync(CreateAccountRequest request, CancellationToken ct = default);
    Task<bool> MergeAccountsAsync(Guid sourceAccountId, Guid targetAccountId, CancellationToken ct = default);

    // Simple Forms
    Task<JournalEntryDto> RecordExpenseAsync(RecordExpenseRequest request, CancellationToken ct = default);
    Task<JournalEntryDto> RecordIncomeAsync(RecordIncomeRequest request, CancellationToken ct = default);
    Task<JournalEntryDto> RecordTransferAsync(RecordTransferRequest request, CancellationToken ct = default);

    // Reports
    Task<MonthlySummaryReport> GetMonthlySummaryAsync(int year, int month, CancellationToken ct = default);
    Task<IReadOnlyList<CategoryBreakdownItem>> GetCategoryBreakdownAsync(int year, int month, AccountType type = AccountType.Expense, CancellationToken ct = default);
    Task<NetWorthReport> GetNetWorthReportAsync(DateOnly? asOf = null, CancellationToken ct = default);
    Task<DimensionalCostReport> GetDimensionalCostReportAsync(Guid? propertyId = null, Guid? assetId = null, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default);

    // Budgets
    Task SetBudgetAsync(SetBudgetRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<BudgetVsActualItem>> GetBudgetVsActualAsync(int year, int month, CancellationToken ct = default);

    // Cheques
    Task<IReadOnlyList<ChequeDto>> GetChequesAsync(ChequeDirection? direction = null, ChequeStatus? status = null, CancellationToken ct = default);
    Task<ChequeDto> CreateChequeAsync(CreateChequeRequest request, CancellationToken ct = default);
    Task<ChequeDto> ClearChequeAsync(Guid chequeId, Guid bankAccountId, CancellationToken ct = default);
    Task<ChequeDto> BounceChequeAsync(Guid chequeId, string reason, CancellationToken ct = default);

    // Loans & Installments
    Task<IReadOnlyList<LoanDto>> GetLoansAsync(LoanDirection? direction = null, CancellationToken ct = default);
    Task<LoanDto> CreateLoanAsync(CreateLoanRequest request, CancellationToken ct = default);
    Task<InstallmentDto> PayInstallmentAsync(Guid installmentId, Guid paymentAccountId, CancellationToken ct = default);
    Task<IReadOnlyList<InstallmentDto>> GetOverdueInstallmentsAsync(CancellationToken ct = default);

    // Reconciliation
    Task<BalanceCheckDto> RecordBalanceCheckAsync(Guid accountId, DateOnly date, long statementBalance, CancellationToken ct = default);
}
