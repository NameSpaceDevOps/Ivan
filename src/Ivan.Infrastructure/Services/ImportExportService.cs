using System.Globalization;
using System.Text.Json;
using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class ImportExportService : IImportExportService
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;
    private readonly ILedgerService _ledgerService;
    private readonly IAuditService _auditService;

    public ImportExportService(
        IIvanDbContext db,
        ICurrentUserContext userContext,
        ILedgerService ledgerService,
        IAuditService auditService)
    {
        _db = db;
        _userContext = userContext;
        _ledgerService = ledgerService;
        _auditService = auditService;
    }

    public async Task<CsvImportResultDto> ImportBooksCsvAsync(Stream csvStream, CancellationToken ct = default)
    {
        using var reader = new StreamReader(csvStream);
        var errors = new List<string>();
        int processed = 0;
        int succeeded = 0;
        int lineNum = 0;

        string? header = await reader.ReadLineAsync(ct);
        // Expected columns: Title,Author,Isbn,Publisher,PublishYear,ShelfLocation

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            lineNum++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            processed++;
            var parts = ParseCsvLine(line);
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]))
            {
                errors.Add($"Line {lineNum}: Title and Author are required.");
                continue;
            }

            try
            {
                int? publishYear = null;
                if (parts.Length > 4 && int.TryParse(parts[4], out var py))
                    publishYear = py;

                var book = new Book
                {
                    HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
                    Title = parts[0].Trim(),
                    Author = parts[1].Trim(),
                    Isbn = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2].Trim() : null,
                    Publisher = parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]) ? parts[3].Trim() : null,
                    PublishYear = publishYear,
                    ShelfLocation = parts.Length > 5 && !string.IsNullOrWhiteSpace(parts[5]) ? parts[5].Trim() : null,
                    Status = BookStatus.Available
                };

                _db.Books.Add(book);
                succeeded++;
            }
            catch (Exception ex)
            {
                errors.Add($"Line {lineNum}: {ex.Message}");
            }
        }

        if (succeeded > 0)
        {
            await _db.SaveChangesAsync(ct);
            await _auditService.LogAsync("ImportBooksCsv", "Book", "", $"Imported {succeeded} books from CSV", ct);
        }

        return new CsvImportResultDto(processed, succeeded, processed - succeeded, errors);
    }

    public async Task<CsvImportResultDto> ImportAssetsCsvAsync(Stream csvStream, CancellationToken ct = default)
    {
        using var reader = new StreamReader(csvStream);
        var errors = new List<string>();
        int processed = 0;
        int succeeded = 0;
        int lineNum = 0;

        string? header = await reader.ReadLineAsync(ct);
        // Expected: Name,Brand,Model,Serial,EstimatedValue

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            lineNum++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            processed++;
            var parts = ParseCsvLine(line);
            if (parts.Length < 1 || string.IsNullOrWhiteSpace(parts[0]))
            {
                errors.Add($"Line {lineNum}: Asset Name is required.");
                continue;
            }

            try
            {
                long? estValue = null;
                if (parts.Length > 4 && long.TryParse(parts[4], out var val))
                    estValue = val;

                var asset = new Asset
                {
                    HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
                    Name = parts[0].Trim(),
                    Brand = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1].Trim() : null,
                    Model = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2].Trim() : null,
                    SerialNo = parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]) ? parts[3].Trim() : null,
                    PurchasePrice = estValue,
                    Status = AssetStatus.InUse
                };

                _db.Assets.Add(asset);
                succeeded++;
            }
            catch (Exception ex)
            {
                errors.Add($"Line {lineNum}: {ex.Message}");
            }
        }

        if (succeeded > 0)
        {
            await _db.SaveChangesAsync(ct);
            await _auditService.LogAsync("ImportAssetsCsv", "Asset", "", $"Imported {succeeded} assets from CSV", ct);
        }

        return new CsvImportResultDto(processed, succeeded, processed - succeeded, errors);
    }

    public async Task<CsvImportResultDto> ImportTransactionsCsvAsync(Stream csvStream, CancellationToken ct = default)
    {
        using var reader = new StreamReader(csvStream);
        var errors = new List<string>();
        int processed = 0;
        int succeeded = 0;
        int lineNum = 0;

        string? header = await reader.ReadLineAsync(ct);
        // Expected: Date,Description,FromAccountCode,ToAccountCode,Amount

        var accounts = await _db.Accounts.ToListAsync(ct);
        var accountMap = accounts.ToDictionary(a => a.Code, a => a.Id, StringComparer.OrdinalIgnoreCase);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            lineNum++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            processed++;
            var parts = ParseCsvLine(line);
            if (parts.Length < 5)
            {
                errors.Add($"Line {lineNum}: Expected Date,Description,FromAccountCode,ToAccountCode,Amount.");
                continue;
            }

            try
            {
                if (!DateOnly.TryParse(parts[0].Trim(), out var date))
                {
                    errors.Add($"Line {lineNum}: Invalid date '{parts[0]}'.");
                    continue;
                }

                var desc = parts[1].Trim();
                var fromCode = parts[2].Trim();
                var toCode = parts[3].Trim();

                if (!long.TryParse(parts[4].Trim(), out var amount) || amount <= 0)
                {
                    errors.Add($"Line {lineNum}: Invalid amount '{parts[4]}'.");
                    continue;
                }

                if (!accountMap.TryGetValue(fromCode, out var fromId))
                {
                    errors.Add($"Line {lineNum}: Unknown account code '{fromCode}'.");
                    continue;
                }

                if (!accountMap.TryGetValue(toCode, out var toId))
                {
                    errors.Add($"Line {lineNum}: Unknown account code '{toCode}'.");
                    continue;
                }

                // Post balanced entry: Debit toId, Credit fromId
                await _ledgerService.PostEntryAsync(new CreateJournalEntryRequest(
                    date,
                    desc,
                    new List<JournalLineItem>
                    {
                        new(toId, amount, 0),
                        new(fromId, 0, amount)
                    }), ct);

                succeeded++;
            }
            catch (Exception ex)
            {
                errors.Add($"Line {lineNum}: {ex.Message}");
            }
        }

        return new CsvImportResultDto(processed, succeeded, processed - succeeded, errors);
    }

    public async Task<string> ExportHouseholdJsonAsync(Guid householdId, CancellationToken ct = default)
    {
        var household = await _db.Households.FindAsync([householdId], ct);
        var people = await _db.People.Where(p => p.HouseholdId == householdId).ToListAsync(ct);
        var properties = await _db.Properties.Include(p => p.Spaces).Where(p => p.HouseholdId == householdId).ToListAsync(ct);
        var assets = await _db.Assets.Where(a => a.HouseholdId == householdId).ToListAsync(ct);
        var tasks = await _db.TaskItems.Where(t => t.HouseholdId == householdId).ToListAsync(ct);
        var books = await _db.Books.Where(b => b.HouseholdId == householdId).ToListAsync(ct);
        var media = await _db.MediaItems.Where(m => m.HouseholdId == householdId).ToListAsync(ct);
        var accounts = await _db.Accounts.Where(a => a.HouseholdId == householdId).ToListAsync(ct);

        var data = new
        {
            ExportedAt = DateTimeOffset.UtcNow,
            Household = household,
            People = people,
            Properties = properties,
            Assets = assets,
            Tasks = tasks,
            Books = books,
            Media = media,
            Accounts = accounts
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };

        return JsonSerializer.Serialize(data, options);
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                {
                    current.Append('\"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }
}
