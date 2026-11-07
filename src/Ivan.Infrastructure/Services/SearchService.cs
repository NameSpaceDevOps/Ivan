using System.Text;
using System.Text.RegularExpressions;
using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class SearchService : ISearchService
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;

    public SearchService(IIvanDbContext db, ICurrentUserContext userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    public string NormalizePersianText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            // Normalize letters
            var c = ch switch
            {
                'ي' => 'ی',
                'ى' => 'ی',
                'ك' => 'ک',
                'ة' => 'ه',
                'ۀ' => 'ه',
                'أ' => 'ا',
                'إ' => 'ا',
                'آ' => 'ا',
                'ؤ' => 'و',
                'ئ' => 'ی',
                // Normalize Persian digits
                '۰' => '0',
                '۱' => '1',
                '۲' => '2',
                '۳' => '3',
                '۴' => '4',
                '۵' => '5',
                '۶' => '6',
                '۷' => '7',
                '۸' => '8',
                '۹' => '9',
                // Normalize Arabic-Indic digits
                '٠' => '0',
                '١' => '1',
                '٢' => '2',
                '٣' => '3',
                '٤' => '4',
                '٥' => '5',
                '٦' => '6',
                '٧' => '7',
                '٨' => '8',
                '٩' => '9',
                _ => ch
            };

            // Skip Persian / Arabic vowels & diacritics
            if (c >= '\u064B' && c <= '\u0652') continue;
            if (c == '\u0654') continue; // Hamza above

            sb.Append(c);
        }

        return sb.ToString().Trim().ToLowerInvariant();
    }

    public async Task<IReadOnlyList<SearchResultDto>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<SearchResultDto>();

        var normalizedQuery = NormalizePersianText(query);
        var results = new List<SearchResultDto>();

        // 1. Tasks
        var tasks = await _db.TaskItems.ToListAsync(ct);
        foreach (var t in tasks)
        {
            if (NormalizePersianText(t.Title).Contains(normalizedQuery) ||
                (!string.IsNullOrEmpty(t.Details) && NormalizePersianText(t.Details).Contains(normalizedQuery)))
            {
                results.Add(new SearchResultDto(
                    t.Id,
                    "Task",
                    t.Title,
                    t.Details,
                    $"/tasks/{t.Id}",
                    t.Status.ToString(),
                    t.DueAt));
            }
        }

        // 2. Assets
        var assets = await _db.Assets.ToListAsync(ct);
        foreach (var a in assets)
        {
            if (NormalizePersianText(a.Name).Contains(normalizedQuery) ||
                (!string.IsNullOrEmpty(a.Brand) && NormalizePersianText(a.Brand).Contains(normalizedQuery)) ||
                (!string.IsNullOrEmpty(a.Model) && NormalizePersianText(a.Model).Contains(normalizedQuery)) ||
                (!string.IsNullOrEmpty(a.SerialNo) && NormalizePersianText(a.SerialNo).Contains(normalizedQuery)))
            {
                results.Add(new SearchResultDto(
                    a.Id,
                    "Asset",
                    a.Name,
                    $"{a.Brand} {a.Model}".Trim(),
                    $"/assets/{a.Id}",
                    a.Status.ToString(),
                    null));
            }
        }

        // 3. People
        var people = await _db.People.ToListAsync(ct);
        foreach (var p in people)
        {
            if (NormalizePersianText(p.DisplayName).Contains(normalizedQuery) ||
                (!string.IsNullOrEmpty(p.FullName) && NormalizePersianText(p.FullName).Contains(normalizedQuery)) ||
                (!string.IsNullOrEmpty(p.Relationship) && NormalizePersianText(p.Relationship).Contains(normalizedQuery)))
            {
                results.Add(new SearchResultDto(
                    p.Id,
                    "Person",
                    p.DisplayName,
                    p.FullName ?? p.Relationship,
                    $"/people/{p.Id}",
                    p.Kind.ToString(),
                    null));
            }
        }

        // 4. Contacts
        var contacts = await _db.Contacts.ToListAsync(ct);
        foreach (var c in contacts)
        {
            if (NormalizePersianText(c.Name).Contains(normalizedQuery) ||
                (!string.IsNullOrEmpty(c.Phones) && NormalizePersianText(c.Phones).Contains(normalizedQuery)) ||
                (!string.IsNullOrEmpty(c.Email) && NormalizePersianText(c.Email).Contains(normalizedQuery)))
            {
                results.Add(new SearchResultDto(
                    c.Id,
                    "Contact",
                    c.Name,
                    $"{c.Category} - {c.Phones}".Trim(),
                    "/places",
                    c.Category.ToString(),
                    null));
            }
        }

        // 5. Books
        var books = await _db.Books.ToListAsync(ct);
        foreach (var b in books)
        {
            if (NormalizePersianText(b.Title).Contains(normalizedQuery) ||
                NormalizePersianText(b.Author).Contains(normalizedQuery) ||
                (!string.IsNullOrEmpty(b.Isbn) && NormalizePersianText(b.Isbn).Contains(normalizedQuery)))
            {
                results.Add(new SearchResultDto(
                    b.Id,
                    "Book",
                    b.Title,
                    b.Author,
                    "/library",
                    b.Status.ToString(),
                    null));
            }
        }

        // 6. Media Items
        var mediaItems = await _db.MediaItems.ToListAsync(ct);
        foreach (var m in mediaItems)
        {
            if (NormalizePersianText(m.Title).Contains(normalizedQuery) ||
                (!string.IsNullOrEmpty(m.Director) && NormalizePersianText(m.Director).Contains(normalizedQuery)))
            {
                results.Add(new SearchResultDto(
                    m.Id,
                    "Media",
                    m.Title,
                    $"{m.Type} - {m.Director}",
                    "/library",
                    m.Type.ToString(),
                    null));
            }
        }

        // 7. Journal Entries (Finance)
        var entries = await _db.JournalEntries.Include(j => j.Lines).ToListAsync(ct);
        foreach (var je in entries)
        {
            if (NormalizePersianText(je.Description).Contains(normalizedQuery))
            {
                var total = je.Lines.Sum(l => l.Debit);
                results.Add(new SearchResultDto(
                    je.Id,
                    "Finance",
                    je.Description,
                    $"{total:N0} IRR",
                    "/money",
                    je.Status.ToString(),
                    new DateTimeOffset(je.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))));
            }
        }

        return results;
    }
}
