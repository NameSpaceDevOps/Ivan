using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class Cheque : BaseTenantEntity
{
    public ChequeDirection Direction { get; set; } = ChequeDirection.Issued;
    public string Number { get; set; } = string.Empty;
    public string? SayadId { get; set; }
    public string Bank { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Currency { get; set; } = "IRR";
    public DateOnly DueDate { get; set; }
    public Guid? CounterpartyContactId { get; set; }
    public ChequeStatus Status { get; set; } = ChequeStatus.Pending;
    public Guid? JournalEntryId { get; set; }

    // Navigation
    public Contact? CounterpartyContact { get; set; }
    public JournalEntry? JournalEntry { get; set; }
}
