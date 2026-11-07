using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class HomeworkItem : BaseTenantEntity
{
    public Guid PersonId { get; set; }
    public Guid SubjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateOnly AssignedOn { get; set; }
    public DateOnly DueOn { get; set; }
    public HomeworkStatus Status { get; set; } = HomeworkStatus.Assigned;
    public bool ParentChecked { get; set; } = false;

    // Navigation
    public Person? Person { get; set; }
    public Subject? Subject { get; set; }
}
