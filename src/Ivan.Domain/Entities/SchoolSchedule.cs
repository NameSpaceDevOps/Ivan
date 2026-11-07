using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class SchoolSchedule : BaseTenantEntity
{
    public Guid PersonId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public int Period { get; set; }
    public Guid SubjectId { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    // Navigation
    public Person? Person { get; set; }
    public Subject? Subject { get; set; }
}
