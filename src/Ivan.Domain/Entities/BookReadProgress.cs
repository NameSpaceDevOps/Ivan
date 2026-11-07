using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class BookReadProgress : BaseTenantEntity
{
    public Guid BookId { get; set; }
    public Guid PersonId { get; set; }
    public ReadStatus Status { get; set; } = ReadStatus.WantToRead;
    public int? CurrentPage { get; set; }
    public DateOnly? StartedAt { get; set; }
    public DateOnly? FinishedAt { get; set; }
    public int? Rating { get; set; }
    public string? Review { get; set; }

    // Navigation
    public Book? Book { get; set; }
    public Person? Person { get; set; }
}
