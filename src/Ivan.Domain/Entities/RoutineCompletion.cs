using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class RoutineCompletion : BaseTenantEntity
{
    public Guid RoutineTaskId { get; set; }
    public DateOnly Date { get; set; }
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ApprovedBy { get; set; }
    public int PointsAwarded { get; set; }
    public CompletionStatus Status { get; set; } = CompletionStatus.PendingApproval;

    // Navigation
    public RoutineTask? RoutineTask { get; set; }
}
