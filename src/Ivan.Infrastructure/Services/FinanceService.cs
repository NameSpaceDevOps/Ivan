using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class FinanceService : IFinanceService
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;
    private readonly ILedgerService _ledgerService;
    private readonly IReminderService _reminderService;
    private readonly IAuditService _auditService;

    public FinanceService(
        IIvanDbContext db,
        ICurrentUserContext userContext,
        ILedgerService ledgerService,
        IReminderService reminderService,
        IAuditService auditService)
    {
        _db = db;
        _userContext = userContext;
        _ledgerService = ledgerService;
        _reminderService = reminderService;
        _auditService = auditService;
    }

    public async Task<IReadOnlyList<AccountDto>> GetAccountsAsync(AccountType? type = null, bool includeInactive = false, CancellationToken ct = default)
    {
        var query = _db.Accounts.AsQueryable();

        if (type.HasValue)
            query = query.Where(a => a.Type == type.Value);

        if (!includeInactive)
            query = query.Where(a => a.IsActive);

        var list = await query.OrderBy(a => a.Code).ToListAsync(ct);

        var result = new List<AccountDto>();
        foreach (var a in list)
        {
            long balance = 0;
            if (!a.IsGroup)
            {
                balance = await _ledgerService.GetAccountBalanceAsync(a.Id, null, ct);
            }
            result.Add(new AccountDto(
                a.Id,
                a.Code,
                a.Name,
                a.PersianName,
                a.Type,
                a.SubType,
                a.Currency,
                a.ParentAccountId,
                a.OwnerPersonId,
                a.Visibility,
                a.BankInfo,
                a.IsActive,
                a.IsSystem,
                a.IsGroup,
                balance));
        }

        return result;
    }

    public async Task<AccountDto> CreateAccountAsync(CreateAccountRequest request, CancellationToken ct = default)
    {
        var account = new Account
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            Code = request.Code,
            Name = request.Name,
            PersianName = request.PersianName,
            Type = request.Type,
            SubType = request.SubType,
            Currency = request.Currency,
            ParentAccountId = request.ParentAccountId,
            OwnerPersonId = request.OwnerPersonId,
            Visibility = request.Visibility,
            BankInfo = request.BankInfo,
            IsGroup = request.IsGroup,
            IsActive = true
        };

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateAccount", "Account", account.Id.ToString(), $"Created account {account.Code} - {account.PersianName}", ct);

        return new AccountDto(
            account.Id,
            account.Code,
            account.Name,
            account.PersianName,
            account.Type,
            account.SubType,
            account.Currency,
            account.ParentAccountId,
            account.OwnerPersonId,
            account.Visibility,
            account.BankInfo,
            account.IsActive,
            account.IsSystem,
            account.IsGroup,
            0);
    }

    public async Task<bool> MergeAccountsAsync(Guid sourceAccountId, Guid targetAccountId, CancellationToken ct = default)
    {
        var source = await _db.Accounts.FindAsync([sourceAccountId], ct)
            ?? throw new KeyNotFoundException($"Source account {sourceAccountId} not found.");
        var target = await _db.Accounts.FindAsync([targetAccountId], ct)
            ?? throw new KeyNotFoundException($"Target account {targetAccountId} not found.");

        if (source.IsSystem)
            throw new InvalidOperationException("System accounts cannot be merged.");

        // Move all journal lines to target
        var lines = await _db.JournalLines.Where(l => l.AccountId == sourceAccountId).ToListAsync(ct);
        foreach (var line in lines)
        {
            line.AccountId = targetAccountId;
        }

        // Deactivate source
        source.IsActive = false;
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("MergeAccounts", "Account", sourceAccountId.ToString(), $"Merged {source.Code} into {target.Code} ({lines.Count} lines moved)", ct);

        return true;
    }

    public async Task<JournalEntryDto> RecordExpenseAsync(RecordExpenseRequest request, CancellationToken ct = default)
    {
        var lines = new List<JournalLineItem>
        {
            // Debit Expense Category
            new(request.ExpenseAccountId, request.Amount, 0, PropertyId: request.PropertyId, PersonId: request.PersonId, AssetId: request.AssetId, Memo: request.Description),
            // Credit Payment Source (Bank/Cash/Card)
            new(request.PaymentAccountId, 0, request.Amount, Memo: $"Payment for: {request.Description}")
        };

        return await _ledgerService.PostEntryAsync(new CreateJournalEntryRequest(
            request.Date,
            request.Description,
            lines,
            Reference: request.Reference,
            SourceType: "Expense"), ct);
    }

    public async Task<JournalEntryDto> RecordIncomeAsync(RecordIncomeRequest request, CancellationToken ct = default)
    {
        var lines = new List<JournalLineItem>
        {
            // Debit Destination (Bank/Cash)
            new(request.DestinationAccountId, request.Amount, 0, Memo: $"Receipt: {request.Description}"),
            // Credit Income Category
            new(request.IncomeAccountId, 0, request.Amount, PersonId: request.PersonId, Memo: request.Description)
        };

        return await _ledgerService.PostEntryAsync(new CreateJournalEntryRequest(
            request.Date,
            request.Description,
            lines,
            Reference: request.Reference,
            SourceType: "Income"), ct);
    }

    public async Task<JournalEntryDto> RecordTransferAsync(RecordTransferRequest request, CancellationToken ct = default)
    {
        var lines = new List<JournalLineItem>
        {
            // Debit Destination
            new(request.DestinationAccountId, request.Amount, 0, Memo: $"Transfer in: {request.Description}"),
            // Credit Source
            new(request.SourceAccountId, 0, request.Amount, Memo: $"Transfer out: {request.Description}")
        };

        return await _ledgerService.PostEntryAsync(new CreateJournalEntryRequest(
            request.Date,
            request.Description,
            lines,
            Reference: request.Reference,
            SourceType: "Transfer"), ct);
    }

    public async Task<MonthlySummaryReport> GetMonthlySummaryAsync(int year, int month, CancellationToken ct = default)
    {
        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var query = _db.JournalLines
            .Include(l => l.Entry)
            .Include(l => l.Account)
            .Where(l => l.Entry != null && l.Entry.Date >= startDate && l.Entry.Date <= endDate);

        var lines = await query.ToListAsync(ct);

        long income = lines.Where(l => l.Account?.Type == AccountType.Income).Sum(l => l.Credit - l.Debit);
        long expense = lines.Where(l => l.Account?.Type == AccountType.Expense).Sum(l => l.Debit - l.Credit);
        long savings = income - expense;
        double savingsPercent = income > 0 ? Math.Round((double)savings / income * 100, 1) : 0;

        return new MonthlySummaryReport(year, month, income, expense, savings, savingsPercent);
    }

    public async Task<IReadOnlyList<CategoryBreakdownItem>> GetCategoryBreakdownAsync(int year, int month, AccountType type = AccountType.Expense, CancellationToken ct = default)
    {
        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var query = _db.JournalLines
            .Include(l => l.Entry)
            .Include(l => l.Account)
            .Where(l => l.Entry != null && l.Entry.Date >= startDate && l.Entry.Date <= endDate && l.Account != null && l.Account.Type == type);

        var lines = await query.ToListAsync(ct);

        var grouped = lines
            .GroupBy(l => l.AccountId)
            .Select(g =>
            {
                var first = g.First().Account!;
                long net = type == AccountType.Expense
                    ? g.Sum(l => l.Debit - l.Credit)
                    : g.Sum(l => l.Credit - l.Debit);
                return new { Account = first, Net = net };
            })
            .Where(x => x.Net > 0)
            .OrderByDescending(x => x.Net)
            .ToList();

        long total = grouped.Sum(x => x.Net);

        return grouped.Select(x => new CategoryBreakdownItem(
            x.Account.Id,
            x.Account.Code,
            x.Account.PersianName,
            x.Net,
            total > 0 ? Math.Round((double)x.Net / total * 100, 1) : 0)).ToList();
    }

    public async Task<NetWorthReport> GetNetWorthReportAsync(DateOnly? asOf = null, CancellationToken ct = default)
    {
        var accounts = await _db.Accounts
            .Where(a => !a.IsGroup && (a.Type == AccountType.Asset || a.Type == AccountType.Liability))
            .ToListAsync(ct);

        long assets = 0;
        long liabilities = 0;

        foreach (var a in accounts)
        {
            long bal = await _ledgerService.GetAccountBalanceAsync(a.Id, asOf, ct);
            if (a.Type == AccountType.Asset)
                assets += bal;
            else if (a.Type == AccountType.Liability)
                liabilities += bal;
        }

        return new NetWorthReport(assets, liabilities, assets - liabilities);
    }

    public async Task<DimensionalCostReport> GetDimensionalCostReportAsync(Guid? propertyId = null, Guid? assetId = null, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default)
    {
        var query = _db.JournalLines
            .Include(l => l.Entry)
            .Include(l => l.Account)
            .Where(l => l.Entry != null && l.Account != null && l.Account.Type == AccountType.Expense);

        if (propertyId.HasValue)
            query = query.Where(l => l.PropertyId == propertyId.Value);

        if (assetId.HasValue)
            query = query.Where(l => l.AssetId == assetId.Value);

        if (from.HasValue)
            query = query.Where(l => l.Entry!.Date >= from.Value);

        if (to.HasValue)
            query = query.Where(l => l.Entry!.Date <= to.Value);

        var list = await query.ToListAsync(ct);
        long total = list.Sum(l => l.Debit - l.Credit);

        return new DimensionalCostReport(propertyId, assetId, total, list.Count);
    }

    public async Task SetBudgetAsync(SetBudgetRequest request, CancellationToken ct = default)
    {
        var budget = await _db.Budgets
            .FirstOrDefaultAsync(b => b.CategoryAccountId == request.CategoryAccountId && b.StartsOn == request.StartsOn && b.Period == request.Period, ct);

        if (budget == null)
        {
            budget = new Budget
            {
                HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
                CategoryAccountId = request.CategoryAccountId,
                Period = request.Period,
                Amount = request.Amount,
                PropertyId = request.PropertyId,
                StartsOn = request.StartsOn,
                EndsOn = request.EndsOn
            };
            _db.Budgets.Add(budget);
        }
        else
        {
            budget.Amount = request.Amount;
            budget.EndsOn = request.EndsOn;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<BudgetVsActualItem>> GetBudgetVsActualAsync(int year, int month, CancellationToken ct = default)
    {
        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var budgets = await _db.Budgets
            .Include(b => b.CategoryAccount)
            .Where(b => b.StartsOn <= startDate && (b.EndsOn == null || b.EndsOn >= endDate))
            .ToListAsync(ct);

        var actualLines = await _db.JournalLines
            .Include(l => l.Entry)
            .Where(l => l.Entry != null && l.Entry.Date >= startDate && l.Entry.Date <= endDate)
            .ToListAsync(ct);

        var result = new List<BudgetVsActualItem>();
        foreach (var b in budgets)
        {
            long actual = actualLines.Where(l => l.AccountId == b.CategoryAccountId).Sum(l => l.Debit - l.Credit);
            long variance = b.Amount - actual;
            double pct = b.Amount > 0 ? Math.Round((double)actual / b.Amount * 100, 1) : 0;

            result.Add(new BudgetVsActualItem(
                b.CategoryAccountId,
                b.CategoryAccount?.PersianName ?? b.CategoryAccount?.Name ?? "",
                b.Amount,
                actual,
                variance,
                pct));
        }

        return result;
    }

    public async Task<IReadOnlyList<ChequeDto>> GetChequesAsync(ChequeDirection? direction = null, ChequeStatus? status = null, CancellationToken ct = default)
    {
        var query = _db.Cheques
            .Include(c => c.CounterpartyContact)
            .AsQueryable();

        if (direction.HasValue)
            query = query.Where(c => c.Direction == direction.Value);

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        var list = await query.OrderBy(c => c.DueDate).ToListAsync(ct);
        return list.Select(c => new ChequeDto(
            c.Id,
            c.Direction,
            c.Number,
            c.SayadId,
            c.Bank,
            c.Amount,
            c.Currency,
            c.DueDate,
            c.CounterpartyContactId,
            c.CounterpartyContact?.Name,
            c.Status,
            c.JournalEntryId)).ToList();
    }

    public async Task<ChequeDto> CreateChequeAsync(CreateChequeRequest request, CancellationToken ct = default)
    {
        var cheque = new Cheque
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            Direction = request.Direction,
            Number = request.Number,
            Bank = request.Bank,
            Amount = request.Amount,
            Currency = request.Currency,
            DueDate = request.DueDate,
            SayadId = request.SayadId,
            CounterpartyContactId = request.CounterpartyContactId,
            Status = ChequeStatus.Pending
        };

        _db.Cheques.Add(cheque);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateCheque", "Cheque", cheque.Id.ToString(), $"Registered {cheque.Direction} cheque #{cheque.Number} for {cheque.Amount:N0}", ct);

        // Schedule reminder for due date
        var dueDateTime = new DateTimeOffset(cheque.DueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var reminderTime = dueDateTime.AddDays(-2);
        if (reminderTime > DateTimeOffset.UtcNow)
        {
            await _reminderService.ScheduleReminderAsync(new ScheduleReminderRequest(
                "ChequeDue",
                cheque.Id,
                $"Cheque #{cheque.Number} due on {cheque.DueDate}",
                reminderTime,
                $"cheque-{cheque.Id}"), ct);
        }

        var contact = request.CounterpartyContactId.HasValue ? await _db.Contacts.FindAsync([request.CounterpartyContactId.Value], ct) : null;
        return new ChequeDto(
            cheque.Id,
            cheque.Direction,
            cheque.Number,
            cheque.SayadId,
            cheque.Bank,
            cheque.Amount,
            cheque.Currency,
            cheque.DueDate,
            cheque.CounterpartyContactId,
            contact?.Name,
            cheque.Status,
            null);
    }

    public async Task<ChequeDto> ClearChequeAsync(Guid chequeId, Guid bankAccountId, CancellationToken ct = default)
    {
        var cheque = await _db.Cheques.FindAsync([chequeId], ct)
            ?? throw new KeyNotFoundException($"Cheque {chequeId} not found.");

        if (cheque.Status == ChequeStatus.Cleared)
            throw new InvalidOperationException("Cheque is already cleared.");

        // Post ledger entry for clearance
        // If Received: Debit Bank, Credit Cheques Received (find account 1102 or default receivable)
        // If Issued: Debit Cheques Issued (account 2101 or default payable), Credit Bank
        var pendingAccount = cheque.Direction == ChequeDirection.Received
            ? await _db.Accounts.FirstOrDefaultAsync(a => a.Code == "1102" || a.SubType == AccountSubType.Receivable, ct)
            : await _db.Accounts.FirstOrDefaultAsync(a => a.Code == "2101" || a.SubType == AccountSubType.Payable, ct);

        if (pendingAccount == null)
            throw new InvalidOperationException("Pending cheques account not found in chart of accounts.");

        var lines = new List<JournalLineItem>();
        if (cheque.Direction == ChequeDirection.Received)
        {
            // Debit Bank, Credit Cheques Received
            lines.Add(new(bankAccountId, cheque.Amount, 0, Memo: $"Deposit of cheque #{cheque.Number}"));
            lines.Add(new(pendingAccount.Id, 0, cheque.Amount, Memo: $"Cleared cheque #{cheque.Number}"));
        }
        else
        {
            // Debit Cheques Issued, Credit Bank
            lines.Add(new(pendingAccount.Id, cheque.Amount, 0, Memo: $"Cleared cheque issued #{cheque.Number}"));
            lines.Add(new(bankAccountId, 0, cheque.Amount, Memo: $"Payment of cheque #{cheque.Number}"));
        }

        var entry = await _ledgerService.PostEntryAsync(new CreateJournalEntryRequest(
            cheque.DueDate,
            $"Cheque clearance #{cheque.Number} ({cheque.Bank})",
            lines,
            Reference: cheque.Number,
            SourceType: "Cheque",
            SourceId: cheque.Id), ct);

        cheque.Status = ChequeStatus.Cleared;
        cheque.JournalEntryId = entry.Id;
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("ClearCheque", "Cheque", cheque.Id.ToString(), $"Cleared cheque #{cheque.Number}", ct);

        var contact = cheque.CounterpartyContactId.HasValue ? await _db.Contacts.FindAsync([cheque.CounterpartyContactId.Value], ct) : null;
        return new ChequeDto(
            cheque.Id,
            cheque.Direction,
            cheque.Number,
            cheque.SayadId,
            cheque.Bank,
            cheque.Amount,
            cheque.Currency,
            cheque.DueDate,
            cheque.CounterpartyContactId,
            contact?.Name,
            cheque.Status,
            cheque.JournalEntryId);
    }

    public async Task<ChequeDto> BounceChequeAsync(Guid chequeId, string reason, CancellationToken ct = default)
    {
        var cheque = await _db.Cheques.FindAsync([chequeId], ct)
            ?? throw new KeyNotFoundException($"Cheque {chequeId} not found.");

        cheque.Status = ChequeStatus.Bounced;
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("BounceCheque", "Cheque", cheque.Id.ToString(), $"Cheque #{cheque.Number} marked bounced: {reason}", ct);

        var contact = cheque.CounterpartyContactId.HasValue ? await _db.Contacts.FindAsync([cheque.CounterpartyContactId.Value], ct) : null;
        return new ChequeDto(
            cheque.Id,
            cheque.Direction,
            cheque.Number,
            cheque.SayadId,
            cheque.Bank,
            cheque.Amount,
            cheque.Currency,
            cheque.DueDate,
            cheque.CounterpartyContactId,
            contact?.Name,
            cheque.Status,
            cheque.JournalEntryId);
    }

    public async Task<IReadOnlyList<LoanDto>> GetLoansAsync(LoanDirection? direction = null, CancellationToken ct = default)
    {
        var query = _db.Loans
            .Include(l => l.CounterpartyContact)
            .Include(l => l.Installments)
            .AsQueryable();

        if (direction.HasValue)
            query = query.Where(l => l.Direction == direction.Value);

        var list = await query.OrderByDescending(l => l.StartDate).ToListAsync(ct);

        return list.Select(l =>
        {
            int paidCount = l.Installments.Count(i => i.Status == InstallmentStatus.Paid);
            long remaining = l.Installments.Where(i => i.Status != InstallmentStatus.Paid).Sum(i => i.Amount - i.PaidAmount);
            var instDtos = l.Installments.OrderBy(i => i.No).Select(i => new InstallmentDto(
                i.Id,
                i.LoanId,
                i.No,
                i.DueDate,
                i.Amount,
                i.PaidAmount,
                i.PaidAt,
                i.JournalEntryId,
                i.Status)).ToList();

            return new LoanDto(
                l.Id,
                l.Direction,
                l.Principal,
                l.Currency,
                l.StartDate,
                l.InterestRate,
                l.InterestFree,
                l.InstallmentCount,
                paidCount,
                remaining,
                l.CounterpartyContactId,
                l.CounterpartyContact?.Name,
                instDtos);
        }).ToList();
    }

    public async Task<LoanDto> CreateLoanAsync(CreateLoanRequest request, CancellationToken ct = default)
    {
        var loan = new Loan
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            Direction = request.Direction,
            Principal = request.Principal,
            StartDate = request.StartDate,
            InstallmentCount = request.InstallmentCount,
            CounterpartyContactId = request.CounterpartyContactId,
            InterestFree = request.InterestFree,
            InterestRate = request.InterestRate,
            Currency = request.Currency
        };

        // Generate installment schedule (monthly)
        for (int i = 1; i <= request.InstallmentCount; i++)
        {
            var dueDate = request.StartDate.AddMonths(i);
            loan.Installments.Add(new Installment
            {
                HouseholdId = loan.HouseholdId,
                No = i,
                DueDate = dueDate,
                Amount = request.InstallmentAmount,
                Status = InstallmentStatus.Pending
            });
        }

        _db.Loans.Add(loan);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateLoan", "Loan", loan.Id.ToString(), $"Created {loan.Direction} loan for {loan.Principal:N0} with {loan.InstallmentCount} installments", ct);

        var contact = await _db.Contacts.FindAsync([loan.CounterpartyContactId], ct);
        var instDtos = loan.Installments.OrderBy(i => i.No).Select(i => new InstallmentDto(
            i.Id,
            i.LoanId,
            i.No,
            i.DueDate,
            i.Amount,
            i.PaidAmount,
            i.PaidAt,
            i.JournalEntryId,
            i.Status)).ToList();

        return new LoanDto(
            loan.Id,
            loan.Direction,
            loan.Principal,
            loan.Currency,
            loan.StartDate,
            loan.InterestRate,
            loan.InterestFree,
            loan.InstallmentCount,
            0,
            loan.Installments.Sum(i => i.Amount),
            loan.CounterpartyContactId,
            contact?.Name,
            instDtos);
    }

    public async Task<InstallmentDto> PayInstallmentAsync(Guid installmentId, Guid paymentAccountId, CancellationToken ct = default)
    {
        var installment = await _db.Installments
            .Include(i => i.Loan)
            .FirstOrDefaultAsync(i => i.Id == installmentId, ct)
            ?? throw new KeyNotFoundException($"Installment {installmentId} not found.");

        if (installment.Status == InstallmentStatus.Paid)
            throw new InvalidOperationException("Installment is already paid.");

        // Post ledger entry:
        // If Borrowed loan: Debit Loan Liability (2001 or default loan), Credit Bank/Cash
        // If Lent loan: Debit Bank/Cash, Credit Loan Receivable (1101 or default receivable)
        var loanAccount = installment.Loan?.Direction == LoanDirection.Borrowed
            ? await _db.Accounts.FirstOrDefaultAsync(a => a.Code == "2001" || a.SubType == AccountSubType.Loan, ct)
            : await _db.Accounts.FirstOrDefaultAsync(a => a.Code == "1101" || a.SubType == AccountSubType.Receivable, ct);

        if (loanAccount == null)
            throw new InvalidOperationException("Loan account not found in chart of accounts.");

        var lines = new List<JournalLineItem>();
        if (installment.Loan?.Direction == LoanDirection.Borrowed)
        {
            lines.Add(new(loanAccount.Id, installment.Amount, 0, Memo: $"Loan payment installment #{installment.No}"));
            lines.Add(new(paymentAccountId, 0, installment.Amount, Memo: $"Payment for loan installment #{installment.No}"));
        }
        else
        {
            lines.Add(new(paymentAccountId, installment.Amount, 0, Memo: $"Received loan installment #{installment.No}"));
            lines.Add(new(loanAccount.Id, 0, installment.Amount, Memo: $"Repayment of lent loan installment #{installment.No}"));
        }

        var entry = await _ledgerService.PostEntryAsync(new CreateJournalEntryRequest(
            DateOnly.FromDateTime(DateTime.UtcNow),
            $"Installment payment #{installment.No}",
            lines,
            Reference: $"INST-{installment.No}",
            SourceType: "Installment",
            SourceId: installment.Id), ct);

        installment.Status = InstallmentStatus.Paid;
        installment.PaidAmount = installment.Amount;
        installment.PaidAt = DateTimeOffset.UtcNow;
        installment.JournalEntryId = entry.Id;

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("PayInstallment", "Installment", installment.Id.ToString(), $"Paid installment #{installment.No} of loan {installment.LoanId}", ct);

        return new InstallmentDto(
            installment.Id,
            installment.LoanId,
            installment.No,
            installment.DueDate,
            installment.Amount,
            installment.PaidAmount,
            installment.PaidAt,
            installment.JournalEntryId,
            installment.Status);
    }

    public async Task<IReadOnlyList<InstallmentDto>> GetOverdueInstallmentsAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var list = await _db.Installments
            .Where(i => i.Status == InstallmentStatus.Pending && i.DueDate < today)
            .OrderBy(i => i.DueDate)
            .ToListAsync(ct);

        return list.Select(i => new InstallmentDto(
            i.Id,
            i.LoanId,
            i.No,
            i.DueDate,
            i.Amount,
            i.PaidAmount,
            i.PaidAt,
            i.JournalEntryId,
            InstallmentStatus.Overdue)).ToList();
    }

    public async Task<BalanceCheckDto> RecordBalanceCheckAsync(Guid accountId, DateOnly date, long statementBalance, CancellationToken ct = default)
    {
        long ledgerBal = await _ledgerService.GetAccountBalanceAsync(accountId, date, ct);
        long diff = statementBalance - ledgerBal;

        var check = new BalanceCheck
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            AccountId = accountId,
            Date = date,
            StatementBalance = statementBalance,
            Difference = diff
        };

        _db.BalanceChecks.Add(check);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("RecordBalanceCheck", "BalanceCheck", check.Id.ToString(), $"Reconciled account {accountId}: diff = {diff:N0}", ct);

        return new BalanceCheckDto(check.Id, check.AccountId, check.Date, check.StatementBalance, ledgerBal, diff);
    }
}
