using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class LedgerService : ILedgerService
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;
    private readonly IAuditService _auditService;

    public LedgerService(
        IIvanDbContext db,
        ICurrentUserContext userContext,
        IAuditService auditService)
    {
        _db = db;
        _userContext = userContext;
        _auditService = auditService;
    }

    public async Task<JournalEntryDto> PostEntryAsync(CreateJournalEntryRequest request, CancellationToken ct = default)
    {
        if (request.Lines == null || request.Lines.Count < 2)
            throw new InvalidOperationException("A journal entry must contain at least two lines.");

        long totalDebit = request.Lines.Sum(l => l.Debit);
        long totalCredit = request.Lines.Sum(l => l.Credit);

        if (totalDebit != totalCredit)
            throw new InvalidOperationException($"Journal entry is not balanced. Total Debits: {totalDebit}, Total Credits: {totalCredit}");

        if (totalDebit <= 0)
            throw new InvalidOperationException("Journal entry total amount must be strictly greater than zero.");

        foreach (var line in request.Lines)
        {
            if (line.Debit < 0 || line.Credit < 0)
                throw new InvalidOperationException("Debit and Credit amounts must be non-negative.");

            if (line.Debit == 0 && line.Credit == 0)
                throw new InvalidOperationException("Journal line must have either a non-zero Debit or Credit.");
        }

        var householdId = _userContext.HouseholdId ?? Guid.Empty;

        var entry = new JournalEntry
        {
            HouseholdId = householdId,
            Date = request.Date,
            Description = request.Description,
            Reference = request.Reference,
            Status = JournalEntryStatus.Posted,
            SourceType = request.SourceType,
            SourceId = request.SourceId
        };

        foreach (var l in request.Lines)
        {
            entry.Lines.Add(new JournalLine
            {
                HouseholdId = householdId,
                AccountId = l.AccountId,
                Debit = l.Debit,
                Credit = l.Credit,
                Currency = l.Currency ?? "IRR",
                ExchangeRate = l.ExchangeRate ?? 1.0m,
                PropertyId = l.PropertyId,
                PersonId = l.PersonId,
                AssetId = l.AssetId,
                Memo = l.Memo
            });
        }

        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("PostJournalEntry", "JournalEntry", entry.Id.ToString(), $"Posted journal entry: {entry.Description} ({totalDebit:N0})", ct);

        return await GetEntryDtoAsync(entry.Id, ct);
    }

    public async Task<JournalEntryDto> VoidEntryAsync(Guid entryId, string reason, CancellationToken ct = default)
    {
        var entry = await _db.JournalEntries
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == entryId, ct)
            ?? throw new KeyNotFoundException($"Journal entry {entryId} not found.");

        if (entry.Status == JournalEntryStatus.Voided)
            throw new InvalidOperationException($"Journal entry {entryId} is already voided.");

        // Mark original entry as voided
        entry.Status = JournalEntryStatus.Voided;

        var householdId = _userContext.HouseholdId ?? Guid.Empty;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Create reversing entry
        var reversal = new JournalEntry
        {
            HouseholdId = householdId,
            Date = today,
            Description = $"[Reversal] {entry.Description} — {reason}",
            Reference = $"REV-{entry.Id.ToString()[..8]}",
            Status = JournalEntryStatus.Posted,
            ReversalOfId = entry.Id,
            SourceType = "Reversal",
            SourceId = entry.Id
        };

        foreach (var line in entry.Lines)
        {
            reversal.Lines.Add(new JournalLine
            {
                HouseholdId = householdId,
                AccountId = line.AccountId,
                Debit = line.Credit,  // Swapped!
                Credit = line.Debit,  // Swapped!
                Currency = line.Currency,
                ExchangeRate = line.ExchangeRate,
                PropertyId = line.PropertyId,
                PersonId = line.PersonId,
                AssetId = line.AssetId,
                Memo = $"Reversal of line: {line.Memo}"
            });
        }

        _db.JournalEntries.Add(reversal);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("VoidJournalEntry", "JournalEntry", entry.Id.ToString(), $"Voided entry {entry.Id} via reversal {reversal.Id}", ct);

        return await GetEntryDtoAsync(reversal.Id, ct);
    }

    public async Task<long> GetAccountBalanceAsync(Guid accountId, DateOnly? asOf = null, CancellationToken ct = default)
    {
        var account = await _db.Accounts.FindAsync([accountId], ct)
            ?? throw new KeyNotFoundException($"Account {accountId} not found.");

        long totalDebit;
        long totalCredit;

        if (!asOf.HasValue)
        {
            var lineTotals = await _db.JournalLines
                .Where(l => l.AccountId == accountId)
                .GroupBy(l => 1)
                .Select(g => new { Debit = g.Sum(l => l.Debit), Credit = g.Sum(l => l.Credit) })
                .FirstOrDefaultAsync(ct);

            totalDebit = lineTotals?.Debit ?? 0;
            totalCredit = lineTotals?.Credit ?? 0;
        }
        else
        {
            var query = _db.JournalLines
                .Where(l => l.AccountId == accountId && l.Entry != null && l.Entry.Date <= asOf.Value);

            var lineTotals = await query
                .GroupBy(l => 1)
                .Select(g => new { Debit = g.Sum(l => l.Debit), Credit = g.Sum(l => l.Credit) })
                .FirstOrDefaultAsync(ct);

            totalDebit = lineTotals?.Debit ?? 0;
            totalCredit = lineTotals?.Credit ?? 0;
        }

        return account.Type switch
        {
            AccountType.Asset or AccountType.Expense => totalDebit - totalCredit,
            _ => totalCredit - totalDebit
        };
    }

    public async Task<IReadOnlyList<TrialBalanceRow>> GetTrialBalanceAsync(DateOnly? asOf = null, CancellationToken ct = default)
    {
        var accounts = await _db.Accounts
            .Where(a => !a.IsGroup)
            .OrderBy(a => a.Code)
            .ToListAsync(ct);

        Dictionary<Guid, (long Debit, long Credit)> lineTotalsByAccount;
        if (!asOf.HasValue)
        {
            lineTotalsByAccount = await _db.JournalLines
                .GroupBy(l => l.AccountId)
                .Select(g => new { AccountId = g.Key, Debit = g.Sum(l => l.Debit), Credit = g.Sum(l => l.Credit) })
                .ToDictionaryAsync(x => x.AccountId, x => (x.Debit, x.Credit), ct);
        }
        else
        {
            lineTotalsByAccount = await _db.JournalLines
                .Where(l => l.Entry != null && l.Entry.Date <= asOf.Value)
                .GroupBy(l => l.AccountId)
                .Select(g => new { AccountId = g.Key, Debit = g.Sum(l => l.Debit), Credit = g.Sum(l => l.Credit) })
                .ToDictionaryAsync(x => x.AccountId, x => (x.Debit, x.Credit), ct);
        }

        var rows = new List<TrialBalanceRow>();
        foreach (var acc in accounts)
        {
            lineTotalsByAccount.TryGetValue(acc.Id, out var totals);
            long debit = totals.Debit;
            long credit = totals.Credit;
            long net = acc.Type switch
            {
                AccountType.Asset or AccountType.Expense => debit - credit,
                _ => credit - debit
            };

            rows.Add(new TrialBalanceRow(acc.Id, acc.Code, acc.PersianName, acc.Type, debit, credit, net));
        }

        return rows;
    }

    public async Task<IReadOnlyList<JournalEntryDto>> GetEntriesAsync(DateOnly? fromDate = null, DateOnly? toDate = null, Guid? accountId = null, CancellationToken ct = default)
    {
        var query = _db.JournalEntries
            .Include(e => e.Lines)
                .ThenInclude(l => l.Account)
            .AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(e => e.Date >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(e => e.Date <= toDate.Value);

        if (accountId.HasValue)
            query = query.Where(e => e.Lines.Any(l => l.AccountId == accountId.Value));

        var entries = await query.ToListAsync(ct);

        return entries
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt)
            .Select(MapToEntryDto)
            .ToList();
    }

    private async Task<JournalEntryDto> GetEntryDtoAsync(Guid entryId, CancellationToken ct)
    {
        var entry = await _db.JournalEntries
            .Include(e => e.Lines)
                .ThenInclude(l => l.Account)
            .FirstAsync(e => e.Id == entryId, ct);

        return MapToEntryDto(entry);
    }

    private static JournalEntryDto MapToEntryDto(JournalEntry e)
    {
        var linesDto = e.Lines.Select(l => new JournalLineDto(
            l.Id,
            l.AccountId,
            l.Account?.Code ?? "",
            l.Account?.PersianName ?? l.Account?.Name ?? "",
            l.Debit,
            l.Credit,
            l.Currency,
            l.PropertyId,
            l.PersonId,
            l.AssetId,
            l.Memo)).ToList();

        long total = linesDto.Sum(l => l.Debit);

        return new JournalEntryDto(
            e.Id,
            e.Date,
            e.Description,
            e.Reference,
            e.Status,
            e.ReversalOfId,
            e.SourceType,
            e.SourceId,
            total,
            linesDto);
    }
}
