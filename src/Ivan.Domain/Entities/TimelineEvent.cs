using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class TimelineEvent : BaseTenantEntity
{
    public string SubjectType { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public DateOnly EventDate { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
}
