using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class Subject : BaseTenantEntity
{
    public Guid PersonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Teacher { get; set; }

    // Navigation
    public Person? Person { get; set; }
    public ICollection<HomeworkItem> HomeworkItems { get; set; } = new List<HomeworkItem>();
    public ICollection<ExamEvent> Exams { get; set; } = new List<ExamEvent>();
    public ICollection<SchoolSchedule> Schedules { get; set; } = new List<SchoolSchedule>();
}
