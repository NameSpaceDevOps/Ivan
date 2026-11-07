using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class Tag : BaseTenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#3b82f6";
}

public class TagLink : BaseTenantEntity
{
    public Guid TagId { get; set; }
    public string TargetType { get; set; } = string.Empty; // e.g. "Task", "Asset", "Contact", "Book"
    public Guid TargetId { get; set; }

    // Navigation
    public Tag? Tag { get; set; }
}
