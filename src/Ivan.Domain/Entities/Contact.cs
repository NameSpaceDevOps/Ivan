using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class Contact : BaseTenantEntity
{
    public string Name { get; set; } = string.Empty;
    public ContactCategory Category { get; set; } = ContactCategory.Other;
    public string Phones { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public Guid? PropertyId { get; set; }

    // Navigation
    public Property? Property { get; set; }
}
