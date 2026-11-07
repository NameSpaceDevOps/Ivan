using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class Book : BaseTenantEntity
{
    public string Title { get; set; } = string.Empty;
    public string? OriginalTitle { get; set; }
    public string Author { get; set; } = string.Empty;
    public string? Translator { get; set; }
    public string? Publisher { get; set; }
    public int? PublishYear { get; set; }
    public string? Isbn { get; set; }
    public int? PageCount { get; set; }
    public Guid? CoverAttachmentId { get; set; }
    public string? CoverUrl { get; set; }
    public Guid? SpaceId { get; set; }
    public string? ShelfLocation { get; set; }
    public BookStatus Status { get; set; } = BookStatus.Available;
    public string? Notes { get; set; }
    public int? Rating { get; set; }

    // Navigation
    public Space? Space { get; set; }
    public Attachment? CoverAttachment { get; set; }
    public ICollection<BookReadProgress> ReadProgresses { get; set; } = new List<BookReadProgress>();
    public ICollection<BookLoan> Loans { get; set; } = new List<BookLoan>();
}
