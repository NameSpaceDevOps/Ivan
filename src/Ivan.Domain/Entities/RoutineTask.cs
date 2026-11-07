using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class RoutineTask : BaseTenantEntity
{
    public Guid PersonId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = "star";
    public RoutineTimeSlot TimeSlot { get; set; } = RoutineTimeSlot.Morning;
    public string Days { get; set; } = "Sat,Sun,Mon,Tue,Wed,Thu,Fri";
    public int Points { get; set; } = 10;
    public bool RequiresApproval { get; set; } = true;
    public bool IsActive { get; set; } = true;

    // Navigation
    public Person? Person { get; set; }
    public ICollection<RoutineCompletion> Completions { get; set; } = new List<RoutineCompletion>();
}
