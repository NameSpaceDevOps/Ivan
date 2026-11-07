using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class LibraryService : ILibraryService
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;
    private readonly IReminderService _reminderService;
    private readonly IAuditService _auditService;

    public LibraryService(
        IIvanDbContext db,
        ICurrentUserContext userContext,
        IReminderService reminderService,
        IAuditService auditService)
    {
        _db = db;
        _userContext = userContext;
        _reminderService = reminderService;
        _auditService = auditService;
    }

    public async Task<IReadOnlyList<BookDto>> GetBooksAsync(string? search = null, BookStatus? status = null, Guid? spaceId = null, CancellationToken ct = default)
    {
        var query = _db.Books
            .Include(b => b.Space)
            .Include(b => b.ReadProgresses)
                .ThenInclude(rp => rp.Person)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(b => b.Title.Contains(s) || b.Author.Contains(s) || (b.Isbn != null && b.Isbn.Contains(s)));
        }

        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        if (spaceId.HasValue)
            query = query.Where(b => b.SpaceId == spaceId.Value);

        var list = await query.ToListAsync(ct);

        return list.OrderBy(b => b.Title).Select(MapBookToDto).ToList();
    }

    public async Task<BookDto?> GetBookByIdAsync(Guid id, CancellationToken ct = default)
    {
        var book = await _db.Books
            .Include(b => b.Space)
            .Include(b => b.ReadProgresses)
                .ThenInclude(rp => rp.Person)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        return book == null ? null : MapBookToDto(book);
    }

    public async Task<BookDto> CreateBookAsync(CreateBookRequest request, CancellationToken ct = default)
    {
        var book = new Book
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            Title = request.Title,
            OriginalTitle = request.OriginalTitle,
            Author = request.Author,
            Translator = request.Translator,
            Publisher = request.Publisher,
            PublishYear = request.PublishYear,
            Isbn = request.Isbn,
            PageCount = request.PageCount,
            CoverAttachmentId = request.CoverAttachmentId,
            CoverUrl = request.CoverUrl,
            SpaceId = request.SpaceId,
            ShelfLocation = request.ShelfLocation,
            Status = request.Status,
            Notes = request.Notes,
            Rating = request.Rating
        };

        _db.Books.Add(book);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateBook", "Book", book.Id.ToString(), $"Created book {book.Title} by {book.Author}", ct);

        return (await GetBookByIdAsync(book.Id, ct))!;
    }

    public async Task<BookDto> UpdateBookAsync(UpdateBookRequest request, CancellationToken ct = default)
    {
        var book = await _db.Books.FirstOrDefaultAsync(b => b.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Book {request.Id} not found.");

        book.Title = request.Title;
        book.OriginalTitle = request.OriginalTitle;
        book.Author = request.Author;
        book.Translator = request.Translator;
        book.Publisher = request.Publisher;
        book.PublishYear = request.PublishYear;
        book.Isbn = request.Isbn;
        book.PageCount = request.PageCount;
        book.CoverAttachmentId = request.CoverAttachmentId;
        book.CoverUrl = request.CoverUrl;
        book.SpaceId = request.SpaceId;
        book.ShelfLocation = request.ShelfLocation;
        book.Status = request.Status;
        book.Notes = request.Notes;
        book.Rating = request.Rating;

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("UpdateBook", "Book", book.Id.ToString(), $"Updated book {book.Title}", ct);

        return (await GetBookByIdAsync(book.Id, ct))!;
    }

    public async Task DeleteBookAsync(Guid id, CancellationToken ct = default)
    {
        var book = await _db.Books.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new KeyNotFoundException($"Book {id} not found.");

        _db.Books.Remove(book);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("DeleteBook", "Book", id.ToString(), $"Deleted book {book.Title}", ct);
    }

    public async Task<BookReadProgressDto> SetReadProgressAsync(SetReadProgressRequest request, CancellationToken ct = default)
    {
        var progress = await _db.BookReadProgresses
            .Include(p => p.Book)
            .Include(p => p.Person)
            .FirstOrDefaultAsync(p => p.BookId == request.BookId && p.PersonId == request.PersonId, ct);

        if (progress == null)
        {
            progress = new BookReadProgress
            {
                HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
                BookId = request.BookId,
                PersonId = request.PersonId,
                Status = request.Status,
                CurrentPage = request.CurrentPage,
                StartedAt = request.StartedAt ?? (request.Status == ReadStatus.Reading ? DateOnly.FromDateTime(DateTime.UtcNow) : null),
                FinishedAt = request.FinishedAt ?? (request.Status == ReadStatus.Finished ? DateOnly.FromDateTime(DateTime.UtcNow) : null),
                Rating = request.Rating,
                Review = request.Review
            };
            _db.BookReadProgresses.Add(progress);
        }
        else
        {
            progress.Status = request.Status;
            progress.CurrentPage = request.CurrentPage;
            if (request.StartedAt.HasValue) progress.StartedAt = request.StartedAt;
            else if (request.Status == ReadStatus.Reading && !progress.StartedAt.HasValue)
                progress.StartedAt = DateOnly.FromDateTime(DateTime.UtcNow);

            if (request.FinishedAt.HasValue) progress.FinishedAt = request.FinishedAt;
            else if (request.Status == ReadStatus.Finished && !progress.FinishedAt.HasValue)
                progress.FinishedAt = DateOnly.FromDateTime(DateTime.UtcNow);

            if (request.Rating.HasValue) progress.Rating = request.Rating;
            if (request.Review != null) progress.Review = request.Review;
        }

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("SetReadProgress", "BookReadProgress", progress.Id.ToString(), $"Updated read status to {request.Status} for book {request.BookId}", ct);

        var book = await _db.Books.FindAsync([progress.BookId], ct);
        var person = await _db.People.FindAsync([progress.PersonId], ct);

        return new BookReadProgressDto(
            progress.Id,
            progress.BookId,
            book?.Title ?? "",
            progress.PersonId,
            person?.DisplayName ?? "",
            progress.Status,
            progress.CurrentPage,
            progress.StartedAt,
            progress.FinishedAt,
            progress.Rating,
            progress.Review);
    }

    public async Task<IReadOnlyList<BookReadProgressDto>> GetReadProgressesAsync(Guid? bookId = null, Guid? personId = null, CancellationToken ct = default)
    {
        var query = _db.BookReadProgresses
            .Include(p => p.Book)
            .Include(p => p.Person)
            .AsQueryable();

        if (bookId.HasValue)
            query = query.Where(p => p.BookId == bookId.Value);

        if (personId.HasValue)
            query = query.Where(p => p.PersonId == personId.Value);

        var list = await query.ToListAsync(ct);

        return list.Select(p => new BookReadProgressDto(
            p.Id,
            p.BookId,
            p.Book?.Title ?? "",
            p.PersonId,
            p.Person?.DisplayName ?? "",
            p.Status,
            p.CurrentPage,
            p.StartedAt,
            p.FinishedAt,
            p.Rating,
            p.Review)).ToList();
    }

    public async Task<BookLoanDto> LoanBookAsync(CreateBookLoanRequest request, CancellationToken ct = default)
    {
        var book = await _db.Books.FirstOrDefaultAsync(b => b.Id == request.BookId, ct)
            ?? throw new KeyNotFoundException($"Book {request.BookId} not found.");

        if (book.Status == BookStatus.Loaned)
            throw new InvalidOperationException($"Book '{book.Title}' is already loaned out.");

        var loanedAt = request.LoanedAt ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var loan = new BookLoan
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            BookId = request.BookId,
            BorrowerName = request.BorrowerName,
            BorrowerContact = request.BorrowerContact,
            BorrowerPersonId = request.BorrowerPersonId,
            LoanedAt = loanedAt,
            DueAt = request.DueAt,
            Status = LoanStatus.Active,
            Notes = request.Notes
        };

        book.Status = BookStatus.Loaned;
        _db.BookLoans.Add(loan);

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("LoanBook", "BookLoan", loan.Id.ToString(), $"Loaned book '{book.Title}' to {loan.BorrowerName} due {loan.DueAt}", ct);

        // Schedule reminder for due date
        var dueDateTime = new DateTimeOffset(loan.DueAt.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc));
        if (dueDateTime > DateTimeOffset.UtcNow)
        {
            await _reminderService.ScheduleReminderAsync(new ScheduleReminderRequest(
                "BookLoanDue",
                loan.Id,
                $"Book due for return: '{book.Title}' borrowed by {loan.BorrowerName}",
                dueDateTime,
                $"bookloan-{loan.Id}"), ct);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new BookLoanDto(
            loan.Id,
            loan.BookId,
            book.Title,
            loan.BorrowerName,
            loan.BorrowerContact,
            loan.BorrowerPersonId,
            loan.LoanedAt,
            loan.DueAt,
            loan.ReturnedAt,
            loan.Status,
            loan.Notes,
            loan.Status == LoanStatus.Active && loan.DueAt < today);
    }

    public async Task<BookLoanDto> ReturnBookAsync(Guid loanId, DateOnly? returnedAt = null, CancellationToken ct = default)
    {
        var loan = await _db.BookLoans
            .Include(l => l.Book)
            .FirstOrDefaultAsync(l => l.Id == loanId, ct)
            ?? throw new KeyNotFoundException($"Book loan {loanId} not found.");

        loan.ReturnedAt = returnedAt ?? DateOnly.FromDateTime(DateTime.UtcNow);
        loan.Status = LoanStatus.Returned;

        if (loan.Book != null)
        {
            loan.Book.Status = BookStatus.Available;
        }

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("ReturnBook", "BookLoan", loan.Id.ToString(), $"Returned book '{loan.Book?.Title}' from {loan.BorrowerName}", ct);

        return new BookLoanDto(
            loan.Id,
            loan.BookId,
            loan.Book?.Title ?? "",
            loan.BorrowerName,
            loan.BorrowerContact,
            loan.BorrowerPersonId,
            loan.LoanedAt,
            loan.DueAt,
            loan.ReturnedAt,
            loan.Status,
            loan.Notes,
            false);
    }

    public async Task<IReadOnlyList<BookLoanDto>> GetLoansAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var query = _db.BookLoans
            .Include(l => l.Book)
            .AsQueryable();

        if (activeOnly)
            query = query.Where(l => l.Status == LoanStatus.Active);

        var list = await query.OrderByDescending(l => l.DueAt).ToListAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return list.Select(l => new BookLoanDto(
            l.Id,
            l.BookId,
            l.Book?.Title ?? "",
            l.BorrowerName,
            l.BorrowerContact,
            l.BorrowerPersonId,
            l.LoanedAt,
            l.DueAt,
            l.ReturnedAt,
            l.Status,
            l.Notes,
            l.Status == LoanStatus.Active && l.DueAt < today)).ToList();
    }

    public async Task<IReadOnlyList<BookLoanDto>> GetWhoHasWhatAsync(CancellationToken ct = default)
    {
        return await GetLoansAsync(activeOnly: true, ct);
    }

    private static BookDto MapBookToDto(Book b)
    {
        var progresses = b.ReadProgresses.Select(rp => new BookReadProgressDto(
            rp.Id,
            rp.BookId,
            b.Title,
            rp.PersonId,
            rp.Person?.DisplayName ?? "",
            rp.Status,
            rp.CurrentPage,
            rp.StartedAt,
            rp.FinishedAt,
            rp.Rating,
            rp.Review)).ToList();

        return new BookDto(
            b.Id,
            b.Title,
            b.OriginalTitle,
            b.Author,
            b.Translator,
            b.Publisher,
            b.PublishYear,
            b.Isbn,
            b.PageCount,
            b.CoverAttachmentId,
            b.CoverUrl,
            b.SpaceId,
            b.Space?.Name,
            b.ShelfLocation,
            b.Status,
            b.Notes,
            b.Rating,
            progresses.Count,
            progresses);
    }
}
