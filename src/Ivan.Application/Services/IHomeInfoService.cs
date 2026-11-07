using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public record UtilityAccountDto(
    Guid Id,
    Guid PropertyId,
    UtilityKind Kind,
    string Provider,
    string AccountRef,
    string? Notes,
    int ReadingCount,
    decimal? LatestReadingValue,
    DateOnly? LatestReadingDate);

public record CreateUtilityAccountRequest(
    Guid PropertyId,
    UtilityKind Kind,
    string Provider,
    string AccountRef,
    string? Notes);

public record MeterReadingDto(
    Guid Id,
    Guid UtilityAccountId,
    DateOnly ReadingDate,
    decimal Value,
    string Unit);

public record RecordMeterReadingRequest(
    Guid UtilityAccountId,
    DateOnly ReadingDate,
    decimal Value,
    string Unit);

public record ContactDto(
    Guid Id,
    string Name,
    ContactCategory Category,
    string Phones,
    string? Email,
    string? Notes,
    Guid? PropertyId);

public record CreateContactRequest(
    string Name,
    ContactCategory Category,
    string Phones,
    string? Email,
    string? Notes,
    Guid? PropertyId);

public interface IHomeInfoService
{
    // Utilities
    Task<List<UtilityAccountDto>> GetUtilityAccountsAsync(Guid propertyId, CancellationToken ct = default);
    Task<UtilityAccountDto> CreateUtilityAccountAsync(CreateUtilityAccountRequest request, CancellationToken ct = default);
    Task DeleteUtilityAccountAsync(Guid accountId, CancellationToken ct = default);

    // Readings
    Task<List<MeterReadingDto>> GetMeterReadingsAsync(Guid utilityAccountId, CancellationToken ct = default);
    Task<MeterReadingDto> RecordMeterReadingAsync(RecordMeterReadingRequest request, CancellationToken ct = default);

    // Contacts
    Task<List<ContactDto>> GetContactsAsync(Guid? propertyId = null, ContactCategory? category = null, CancellationToken ct = default);
    Task<ContactDto> CreateContactAsync(CreateContactRequest request, CancellationToken ct = default);
    Task DeleteContactAsync(Guid contactId, CancellationToken ct = default);
}
