using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class BookLoan : BaseTenantEntity
{
    public Guid BookId { get; set; }
    public string BorrowerName { get; set; } = string.Empty;
    public string? BorrowerContact { get; set; }
    public Guid? BorrowerPersonId { get; set; }
    public DateOnly LoanedAt { get; set; }
    public DateOnly DueAt { get; set; }
    public DateOnly? ReturnedAt { get; set; }
    public LoanStatus Status { get; set; } = LoanStatus.Active;
    public string? Notes { get; set; }

    // Navigation
    public Book? Book { get; set; }
    public Person? BorrowerPerson { get; set; }
}
