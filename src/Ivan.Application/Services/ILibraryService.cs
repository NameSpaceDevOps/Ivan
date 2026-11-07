using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public record BookDto(
    Guid Id,
    string Title,
    string? OriginalTitle,
    string Author,
    string? Translator,
    string? Publisher,
    int? PublishYear,
    string? Isbn,
    int? PageCount,
    Guid? CoverAttachmentId,
    string? CoverUrl,
    Guid? SpaceId,
    string? SpaceName,
    string? ShelfLocation,
    BookStatus Status,
    string? Notes,
    int? Rating,
    int ReadersCount,
    IReadOnlyList<BookReadProgressDto> ReadProgresses);

public record CreateBookRequest(
    string Title,
    string Author,
    string? OriginalTitle = null,
    string? Translator = null,
    string? Publisher = null,
    int? PublishYear = null,
    string? Isbn = null,
    int? PageCount = null,
    Guid? CoverAttachmentId = null,
    string? CoverUrl = null,
    Guid? SpaceId = null,
    string? ShelfLocation = null,
    BookStatus Status = BookStatus.Available,
    string? Notes = null,
    int? Rating = null);

public record UpdateBookRequest(
    Guid Id,
    string Title,
    string Author,
    string? OriginalTitle = null,
    string? Translator = null,
    string? Publisher = null,
    int? PublishYear = null,
    string? Isbn = null,
    int? PageCount = null,
    Guid? CoverAttachmentId = null,
    string? CoverUrl = null,
    Guid? SpaceId = null,
    string? ShelfLocation = null,
    BookStatus Status = BookStatus.Available,
    string? Notes = null,
    int? Rating = null);

public record BookReadProgressDto(
    Guid Id,
    Guid BookId,
    string BookTitle,
    Guid PersonId,
    string PersonName,
    ReadStatus Status,
    int? CurrentPage,
    DateOnly? StartedAt,
    DateOnly? FinishedAt,
    int? Rating,
    string? Review);

public record SetReadProgressRequest(
    Guid BookId,
    Guid PersonId,
    ReadStatus Status,
    int? CurrentPage = null,
    DateOnly? StartedAt = null,
    DateOnly? FinishedAt = null,
    int? Rating = null,
    string? Review = null);

public record BookLoanDto(
    Guid Id,
    Guid BookId,
    string BookTitle,
    string BorrowerName,
    string? BorrowerContact,
    Guid? BorrowerPersonId,
    DateOnly LoanedAt,
    DateOnly DueAt,
    DateOnly? ReturnedAt,
    LoanStatus Status,
    string? Notes,
    bool IsOverdue);

public record CreateBookLoanRequest(
    Guid BookId,
    string BorrowerName,
    DateOnly DueAt,
    string? BorrowerContact = null,
    Guid? BorrowerPersonId = null,
    DateOnly? LoanedAt = null,
    string? Notes = null);

public interface ILibraryService
{
    // Books
    Task<IReadOnlyList<BookDto>> GetBooksAsync(string? search = null, BookStatus? status = null, Guid? spaceId = null, CancellationToken ct = default);
    Task<BookDto?> GetBookByIdAsync(Guid id, CancellationToken ct = default);
    Task<BookDto> CreateBookAsync(CreateBookRequest request, CancellationToken ct = default);
    Task<BookDto> UpdateBookAsync(UpdateBookRequest request, CancellationToken ct = default);
    Task DeleteBookAsync(Guid id, CancellationToken ct = default);

    // Reading Status
    Task<BookReadProgressDto> SetReadProgressAsync(SetReadProgressRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<BookReadProgressDto>> GetReadProgressesAsync(Guid? bookId = null, Guid? personId = null, CancellationToken ct = default);

    // Lending / Loans
    Task<BookLoanDto> LoanBookAsync(CreateBookLoanRequest request, CancellationToken ct = default);
    Task<BookLoanDto> ReturnBookAsync(Guid loanId, DateOnly? returnedAt = null, CancellationToken ct = default);
    Task<IReadOnlyList<BookLoanDto>> GetLoansAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<IReadOnlyList<BookLoanDto>> GetWhoHasWhatAsync(CancellationToken ct = default);
}
