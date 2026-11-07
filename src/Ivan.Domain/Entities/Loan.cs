using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class Loan : BaseTenantEntity
{
    public LoanDirection Direction { get; set; } = LoanDirection.Borrowed;
    public Guid? CounterpartyContactId { get; set; }
    public Guid? CounterpartyPersonId { get; set; }
    public long Principal { get; set; }
    public string Currency { get; set; } = "IRR";
    public DateOnly StartDate { get; set; }
    public decimal? InterestRate { get; set; }
    public bool InterestFree { get; set; } = true;
    public int InstallmentCount { get; set; }

    // Navigation
    public Contact? CounterpartyContact { get; set; }
    public Person? CounterpartyPerson { get; set; }
    public ICollection<Installment> Installments { get; set; } = new List<Installment>();
}
