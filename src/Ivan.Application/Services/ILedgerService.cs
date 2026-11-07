using Ivan.Domain.Entities;
using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public record JournalLineItem(
    Guid AccountId,
    long Debit,
    long Credit,
    string? Currency = null,
    decimal? ExchangeRate = null,
    Guid? PropertyId = null,
    Guid? PersonId = null,
    Guid? AssetId = null,
    string? Memo = null);

public record CreateJournalEntryRequest(
    DateOnly Date,
    string Description,
    IReadOnlyList<JournalLineItem> Lines,
    string? Reference = null,
    string? SourceType = null,
    Guid? SourceId = null);

public record JournalLineDto(
    Guid Id,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    long Debit,
    long Credit,
    string Currency,
    Guid? PropertyId,
    Guid? PersonId,
    Guid? AssetId,
    string? Memo);

public record JournalEntryDto(
    Guid Id,
    DateOnly Date,
    string Description,
    string? Reference,
    JournalEntryStatus Status,
    Guid? ReversalOfId,
    string? SourceType,
    Guid? SourceId,
    long TotalAmount,
    IReadOnlyList<JournalLineDto> Lines);

public record TrialBalanceRow(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    AccountType Type,
    long TotalDebit,
    long TotalCredit,
    long NetBalance);

public interface ILedgerService
{
    Task<JournalEntryDto> PostEntryAsync(CreateJournalEntryRequest request, CancellationToken ct = default);
    Task<JournalEntryDto> VoidEntryAsync(Guid entryId, string reason, CancellationToken ct = default);
    Task<long> GetAccountBalanceAsync(Guid accountId, DateOnly? asOf = null, CancellationToken ct = default);
    Task<IReadOnlyList<TrialBalanceRow>> GetTrialBalanceAsync(DateOnly? asOf = null, CancellationToken ct = default);
    Task<IReadOnlyList<JournalEntryDto>> GetEntriesAsync(DateOnly? fromDate = null, DateOnly? toDate = null, Guid? accountId = null, CancellationToken ct = default);
}
