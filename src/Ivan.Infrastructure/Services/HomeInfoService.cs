using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class HomeInfoService : IHomeInfoService
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;
    private readonly IAuditService _auditService;

    public HomeInfoService(
        IIvanDbContext db,
        ICurrentUserContext userContext,
        IAuditService auditService)
    {
        _db = db;
        _userContext = userContext;
        _auditService = auditService;
    }

    public async Task<List<UtilityAccountDto>> GetUtilityAccountsAsync(Guid propertyId, CancellationToken ct = default)
    {
        var accounts = await _db.UtilityAccounts
            .Include(u => u.MeterReadings)
            .Where(u => u.PropertyId == propertyId)
            .ToListAsync(ct);

        return accounts.Select(u =>
        {
            var latest = u.MeterReadings.OrderByDescending(r => r.ReadingDate).FirstOrDefault();
            return new UtilityAccountDto(
                u.Id,
                u.PropertyId,
                u.Kind,
                u.Provider,
                u.AccountRef,
                u.Notes,
                u.MeterReadings.Count,
                latest?.Value,
                latest?.ReadingDate);
        }).ToList();
    }

    public async Task<UtilityAccountDto> CreateUtilityAccountAsync(CreateUtilityAccountRequest request, CancellationToken ct = default)
    {
        var account = new UtilityAccount
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            PropertyId = request.PropertyId,
            Kind = request.Kind,
            Provider = request.Provider,
            AccountRef = request.AccountRef,
            Notes = request.Notes
        };

        _db.UtilityAccounts.Add(account);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateUtilityAccount", "UtilityAccount", account.Id.ToString(), $"Created {account.Kind} account for {account.Provider}", ct);

        return new UtilityAccountDto(
            account.Id,
            account.PropertyId,
            account.Kind,
            account.Provider,
            account.AccountRef,
            account.Notes,
            0,
            null,
            null);
    }

    public async Task DeleteUtilityAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = await _db.UtilityAccounts.FindAsync([accountId], ct);
        if (account != null)
        {
            _db.UtilityAccounts.Remove(account);
            await _db.SaveChangesAsync(ct);
            await _auditService.LogAsync("DeleteUtilityAccount", "UtilityAccount", accountId.ToString(), $"Deleted utility account {account.Provider}", ct);
        }
    }

    public async Task<List<MeterReadingDto>> GetMeterReadingsAsync(Guid utilityAccountId, CancellationToken ct = default)
    {
        var readings = await _db.MeterReadings
            .Where(r => r.UtilityAccountId == utilityAccountId)
            .ToListAsync(ct);

        return readings
            .OrderByDescending(r => r.ReadingDate)
            .Select(r => new MeterReadingDto(r.Id, r.UtilityAccountId, r.ReadingDate, r.Value, r.Unit))
            .ToList();
    }

    public async Task<MeterReadingDto> RecordMeterReadingAsync(RecordMeterReadingRequest request, CancellationToken ct = default)
    {
        var reading = new MeterReading
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            UtilityAccountId = request.UtilityAccountId,
            ReadingDate = request.ReadingDate,
            Value = request.Value,
            Unit = request.Unit
        };

        _db.MeterReadings.Add(reading);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("RecordMeterReading", "MeterReading", reading.Id.ToString(), $"Recorded reading {reading.Value} {reading.Unit}", ct);

        return new MeterReadingDto(reading.Id, reading.UtilityAccountId, reading.ReadingDate, reading.Value, reading.Unit);
    }

    public async Task<List<ContactDto>> GetContactsAsync(Guid? propertyId = null, ContactCategory? category = null, CancellationToken ct = default)
    {
        var query = _db.Contacts.AsQueryable();

        if (propertyId.HasValue)
            query = query.Where(c => c.PropertyId == propertyId.Value);

        if (category.HasValue)
            query = query.Where(c => c.Category == category.Value);

        var list = await query.ToListAsync(ct);
        return list
            .OrderBy(c => c.Name)
            .Select(c => new ContactDto(c.Id, c.Name, c.Category, c.Phones, c.Email, c.Notes, c.PropertyId))
            .ToList();
    }

    public async Task<ContactDto> CreateContactAsync(CreateContactRequest request, CancellationToken ct = default)
    {
        var contact = new Contact
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            Name = request.Name,
            Category = request.Category,
            Phones = request.Phones,
            Email = request.Email,
            Notes = request.Notes,
            PropertyId = request.PropertyId
        };

        _db.Contacts.Add(contact);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateContact", "Contact", contact.Id.ToString(), $"Created contact {contact.Name} ({contact.Category})", ct);

        return new ContactDto(contact.Id, contact.Name, contact.Category, contact.Phones, contact.Email, contact.Notes, contact.PropertyId);
    }

    public async Task DeleteContactAsync(Guid contactId, CancellationToken ct = default)
    {
        var contact = await _db.Contacts.FindAsync([contactId], ct);
        if (contact != null)
        {
            _db.Contacts.Remove(contact);
            await _db.SaveChangesAsync(ct);
            await _auditService.LogAsync("DeleteContact", "Contact", contactId.ToString(), $"Deleted contact {contact.Name}", ct);
        }
    }
}
