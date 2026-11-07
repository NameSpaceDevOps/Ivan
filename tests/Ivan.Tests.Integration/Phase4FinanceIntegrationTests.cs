using Ivan.Application.Common;
using Ivan.Application.Services;
using Ivan.Domain.Common;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Ivan.Infrastructure.Data;
using Ivan.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ivan.Tests.Integration;

public class Phase4FinanceIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<IvanDbContext> _options;
    private readonly CurrentUserContext _userContext;
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public Phase4FinanceIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<IvanDbContext>()
            .UseSqlite(_connection)
            .Options;

        _userContext = new CurrentUserContext();
        _userContext.SetContext(_userId, _householdId, MembershipRole.Parent);

        using var init = new IvanDbContext(_options, _userContext);
        init.Database.EnsureCreated();

        // Seed household & membership
        var household = new Household
        {
            Id = _householdId,
            Name = "Finance Test Household",
            DefaultCurrency = "IRR"
        };
        var membership = new Membership
        {
            HouseholdId = _householdId,
            UserId = _userId,
            Role = MembershipRole.Parent,
            IsActive = true
        };
        init.Households.Add(household);
        init.Memberships.Add(membership);
        init.SaveChanges();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task Ledger_EnforcesBalanceInvariant_AndSupportsVoidByReversal()
    {
        using var db = new IvanDbContext(_options, _userContext);
        var audit = new AuditService(db, _userContext);
        var ledger = new LedgerService(db, _userContext, audit);

        var bankAccount = new Account
        {
            HouseholdId = _householdId,
            Code = "1011",
            Name = "Checking",
            PersianName = "جاری",
            Type = AccountType.Asset,
            SubType = AccountSubType.Bank
        };
        var expenseAccount = new Account
        {
            HouseholdId = _householdId,
            Code = "5201",
            Name = "Groceries",
            PersianName = "خواربار",
            Type = AccountType.Expense,
            SubType = AccountSubType.Category
        };
        db.Accounts.AddRange(bankAccount, expenseAccount);
        await db.SaveChangesAsync();

        // 1. Unbalanced entry should fail
        var unbalancedLines = new List<JournalLineItem>
        {
            new(expenseAccount.Id, 500_000, 0),
            new(bankAccount.Id, 0, 400_000) // 100,000 difference!
        };
        var unbalancedRequest = new CreateJournalEntryRequest(
            new DateOnly(2026, 1, 15),
            "Unbalanced purchase",
            unbalancedLines);

        await Assert.ThrowsAsync<InvalidOperationException>(() => ledger.PostEntryAsync(unbalancedRequest));

        // 2. Balanced entry should succeed
        var balancedLines = new List<JournalLineItem>
        {
            new(expenseAccount.Id, 500_000, 0),
            new(bankAccount.Id, 0, 500_000)
        };
        var balancedRequest = new CreateJournalEntryRequest(
            new DateOnly(2026, 1, 15),
            "Supermarket purchase",
            balancedLines);

        var entry = await ledger.PostEntryAsync(balancedRequest);
        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal(500_000, entry.TotalAmount);
        Assert.Equal(JournalEntryStatus.Posted, entry.Status);

        // Check balances: Expense is +500,000, Bank is -500,000
        var expenseBal = await ledger.GetAccountBalanceAsync(expenseAccount.Id);
        var bankBal = await ledger.GetAccountBalanceAsync(bankAccount.Id);
        Assert.Equal(500_000, expenseBal);
        Assert.Equal(-500_000, bankBal);

        // 3. Voiding the entry should create reversing entry and return balances to 0
        var voidResult = await ledger.VoidEntryAsync(entry.Id, "Returned items");
        Assert.NotNull(voidResult);
        Assert.Equal(entry.Id, voidResult.ReversalOfId);

        var expenseBalAfterVoid = await ledger.GetAccountBalanceAsync(expenseAccount.Id);
        var bankBalAfterVoid = await ledger.GetAccountBalanceAsync(bankAccount.Id);
        Assert.Equal(0, expenseBalAfterVoid);
        Assert.Equal(0, bankBalAfterVoid);
    }

    [Fact]
    public async Task ChartOfAccounts_SeedingAndMerging_WorkCorrectly()
    {
        using var db = new IvanDbContext(_options, _userContext);
        var audit = new AuditService(db, _userContext);
        var seeder = new ChartOfAccountsSeeder(db);
        var ledger = new LedgerService(db, _userContext, audit);
        var reminder = new ReminderService(db);
        var finance = new FinanceService(db, _userContext, ledger, reminder, audit);

        // Seed chart of accounts (Full preset)
        await seeder.SeedAsync(_householdId, ChartPreset.Full);

        var accounts = await finance.GetAccountsAsync();
        Assert.True(accounts.Count > 20);

        // Verify system accounts exist
        Assert.Contains(accounts, a => a.Code == "3000" && a.IsSystem);
        Assert.Contains(accounts, a => a.Code == "3100" && a.IsSystem);
        Assert.Contains(accounts, a => a.Code == "6990" && a.IsSystem);

        // Test account merge
        var food = accounts.First(a => a.Code == "5201"); // Groceries
        var dining = accounts.First(a => a.Code == "5202"); // Dining out
        var bank = accounts.First(a => a.Code == "1011"); // Checking

        // Post a transaction to dining
        await finance.RecordExpenseAsync(new RecordExpenseRequest(
            new DateOnly(2026, 2, 1),
            300_000,
            dining.Id,
            bank.Id,
            "Dinner"));

        // Merge dining into groceries
        var merged = await finance.MergeAccountsAsync(dining.Id, food.Id);
        Assert.True(merged);

        // Dining should be deactivated and groceries should have the balance
        var diningUpdated = await db.Accounts.FindAsync(dining.Id);
        Assert.False(diningUpdated!.IsActive);

        var foodBal = await ledger.GetAccountBalanceAsync(food.Id);
        Assert.Equal(300_000, foodBal);
    }

    [Fact]
    public async Task SimpleForms_AndDimensionalTracking_WorkCorrectly()
    {
        using var db = new IvanDbContext(_options, _userContext);
        var audit = new AuditService(db, _userContext);
        var seeder = new ChartOfAccountsSeeder(db);
        var ledger = new LedgerService(db, _userContext, audit);
        var reminder = new ReminderService(db);
        var finance = new FinanceService(db, _userContext, ledger, reminder, audit);

        await seeder.SeedAsync(_householdId, ChartPreset.Compact);

        var bank = await db.Accounts.FirstAsync(a => a.Code == "1011");
        var salary = await db.Accounts.FirstAsync(a => a.Code == "4001");
        var electricity = await db.Accounts.FirstAsync(a => a.Code == "5101");
        var wallet = await db.Accounts.FirstAsync(a => a.Code == "1022");

        var property = new Property
        {
            HouseholdId = _householdId,
            Name = "Tehran Villa"
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        // 1. Record Income (Salary: 100,000,000 IRR)
        await finance.RecordIncomeAsync(new RecordIncomeRequest(
            new DateOnly(2026, 3, 1),
            100_000_000,
            salary.Id,
            bank.Id,
            "Monthly salary"));

        var bankBal = await ledger.GetAccountBalanceAsync(bank.Id);
        Assert.Equal(100_000_000, bankBal);

        // 2. Record Expense with Property Dimension (Electricity: 2,500,000 IRR)
        await finance.RecordExpenseAsync(new RecordExpenseRequest(
            new DateOnly(2026, 3, 5),
            2_500_000,
            electricity.Id,
            bank.Id,
            "Villa power bill",
            PropertyId: property.Id));

        bankBal = await ledger.GetAccountBalanceAsync(bank.Id);
        Assert.Equal(97_500_000, bankBal);

        // 3. Dimensional report check
        var dimReport = await finance.GetDimensionalCostReportAsync(property.Id);
        Assert.Equal(2_500_000, dimReport.TotalCost);
        Assert.Equal(1, dimReport.TransactionCount);

        // 4. Record Transfer (Bank to Wallet: 5,000,000 IRR)
        await finance.RecordTransferAsync(new RecordTransferRequest(
            new DateOnly(2026, 3, 10),
            5_000_000,
            bank.Id,
            wallet.Id,
            "Top-up digital wallet"));

        var walletBal = await ledger.GetAccountBalanceAsync(wallet.Id);
        Assert.Equal(5_000_000, walletBal);
        bankBal = await ledger.GetAccountBalanceAsync(bank.Id);
        Assert.Equal(92_500_000, bankBal);

        // 5. Monthly summary report
        var monthlySummary = await finance.GetMonthlySummaryAsync(2026, 3);
        Assert.Equal(100_000_000, monthlySummary.TotalIncome);
        Assert.Equal(2_500_000, monthlySummary.TotalExpense);
        Assert.Equal(97_500_000, monthlySummary.NetSavings);
    }

    [Fact]
    public async Task ChequeFlow_AndClearance_PostsLedgerEntry()
    {
        using var db = new IvanDbContext(_options, _userContext);
        var audit = new AuditService(db, _userContext);
        var seeder = new ChartOfAccountsSeeder(db);
        var ledger = new LedgerService(db, _userContext, audit);
        var reminder = new ReminderService(db);
        var finance = new FinanceService(db, _userContext, ledger, reminder, audit);

        await seeder.SeedAsync(_householdId, ChartPreset.Compact);

        var bank = await db.Accounts.FirstAsync(a => a.Code == "1011");

        // Create received cheque
        var cheque = await finance.CreateChequeAsync(new CreateChequeRequest(
            ChequeDirection.Received,
            "CHQ-778899",
            "Mellat",
            45_000_000,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))));

        Assert.Equal(ChequeStatus.Pending, cheque.Status);

        // Verify reminder created
        var reminderRecord = await db.Reminders.FirstOrDefaultAsync(r => r.SubjectType == "ChequeDue");
        Assert.NotNull(reminderRecord);

        // Clear cheque to bank
        var cleared = await finance.ClearChequeAsync(cheque.Id, bank.Id);
        Assert.Equal(ChequeStatus.Cleared, cleared.Status);
        Assert.NotNull(cleared.JournalEntryId);

        // Check bank balance increased by 45,000,000
        var bankBal = await ledger.GetAccountBalanceAsync(bank.Id);
        Assert.Equal(45_000_000, bankBal);
    }

    [Fact]
    public async Task LoanAndInstallmentSchedule_AndPayment_UpdatesStatusAndLedger()
    {
        using var db = new IvanDbContext(_options, _userContext);
        var audit = new AuditService(db, _userContext);
        var seeder = new ChartOfAccountsSeeder(db);
        var ledger = new LedgerService(db, _userContext, audit);
        var reminder = new ReminderService(db);
        var finance = new FinanceService(db, _userContext, ledger, reminder, audit);

        await seeder.SeedAsync(_householdId, ChartPreset.Compact);

        var bank = await db.Accounts.FirstAsync(a => a.Code == "1011");

        var contact = new Contact
        {
            HouseholdId = _householdId,
            Name = "Bank Melli Branch"
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        // Create loan with 6 installments of 10,000,000 each
        var loan = await finance.CreateLoanAsync(new CreateLoanRequest(
            LoanDirection.Borrowed,
            60_000_000,
            new DateOnly(2026, 1, 1),
            6,
            10_000_000,
            contact.Id));

        Assert.Equal(6, loan.Installments.Count);
        Assert.Equal(60_000_000, loan.RemainingBalance);

        // Pay installment #1
        var firstInst = loan.Installments.First(i => i.No == 1);
        var paidInst = await finance.PayInstallmentAsync(firstInst.Id, bank.Id);

        Assert.Equal(InstallmentStatus.Paid, paidInst.Status);
        Assert.NotNull(paidInst.PaidAt);
        Assert.NotNull(paidInst.JournalEntryId);

        // Check bank reduced by 10,000,000
        var bankBal = await ledger.GetAccountBalanceAsync(bank.Id);
        Assert.Equal(-10_000_000, bankBal);

        // Remaining balance of loan should now be 50,000,000
        var loans = await finance.GetLoansAsync();
        Assert.Equal(50_000_000, loans[0].RemainingBalance);
        Assert.Equal(1, loans[0].PaidInstallmentCount);
    }

    [Fact]
    public async Task BudgetsAndReconciliation_CalculateVarianceAndDifference()
    {
        using var db = new IvanDbContext(_options, _userContext);
        var audit = new AuditService(db, _userContext);
        var seeder = new ChartOfAccountsSeeder(db);
        var ledger = new LedgerService(db, _userContext, audit);
        var reminder = new ReminderService(db);
        var finance = new FinanceService(db, _userContext, ledger, reminder, audit);

        await seeder.SeedAsync(_householdId, ChartPreset.Compact);

        var food = await db.Accounts.FirstAsync(a => a.Code == "5201");
        var bank = await db.Accounts.FirstAsync(a => a.Code == "1011");

        // 1. Set budget for Food in May 2026: 20,000,000 IRR
        await finance.SetBudgetAsync(new SetBudgetRequest(
            food.Id,
            BudgetPeriod.Monthly,
            20_000_000,
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 31)));

        // Record 12,000,000 actual food expense in May
        await finance.RecordExpenseAsync(new RecordExpenseRequest(
            new DateOnly(2026, 5, 10),
            12_000_000,
            food.Id,
            bank.Id,
            "Groceries"));

        var budgetVsActual = await finance.GetBudgetVsActualAsync(2026, 5);
        Assert.Single(budgetVsActual);
        var item = budgetVsActual[0];
        Assert.Equal(20_000_000, item.BudgetAmount);
        Assert.Equal(12_000_000, item.ActualAmount);
        Assert.Equal(8_000_000, item.Variance);
        Assert.Equal(60.0, item.UtilizationPercent);

        // 2. Balance Reconciliation
        // Ledger balance on Bank is -12,000,000
        // Statement balance from bank is -10,000,000 (meaning 2,000,000 difference / unrecorded deposit)
        var check = await finance.RecordBalanceCheckAsync(bank.Id, new DateOnly(2026, 5, 31), -10_000_000);
        Assert.Equal(-10_000_000, check.StatementBalance);
        Assert.Equal(-12_000_000, check.LedgerBalance);
        Assert.Equal(2_000_000, check.Difference);
    }
}
