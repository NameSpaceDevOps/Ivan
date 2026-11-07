using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class Installment : BaseTenantEntity
{
    public Guid LoanId { get; set; }
    public int No { get; set; }
    public DateOnly DueDate { get; set; }
    public long Amount { get; set; }
    public long PaidAmount { get; set; } = 0;
    public DateTimeOffset? PaidAt { get; set; }
    public Guid? JournalEntryId { get; set; }
    public InstallmentStatus Status { get; set; } = InstallmentStatus.Pending;

    // Navigation
    public Loan? Loan { get; set; }
    public JournalEntry? JournalEntry { get; set; }
}
