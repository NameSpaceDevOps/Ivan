using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class PersonDocument : BaseTenantEntity
{
    public Guid PersonId { get; set; }
    public PersonDocumentType Type { get; set; } = PersonDocumentType.NationalId;
    public string? Number { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public ResourceVisibility Visibility { get; set; } = ResourceVisibility.Guardians;

    // Navigation
    public Person? Person { get; set; }
}
