using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class PersonGuardian : BaseTenantEntity
{
    public Guid PersonId { get; set; }
    public Guid GuardianPersonId { get; set; }

    // Navigation
    public Person? Person { get; set; }
    public Person? GuardianPerson { get; set; }
}
