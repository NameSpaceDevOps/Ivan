using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class MediaPersonProgress : BaseTenantEntity
{
    public Guid MediaItemId { get; set; }
    public Guid PersonId { get; set; }
    public WatchStatus WatchStatus { get; set; } = WatchStatus.PlanToWatch;
    public int? Rating { get; set; }
    public string? Review { get; set; }
    public DateTimeOffset? LastWatchedAt { get; set; }

    // Navigation
    public MediaItem? MediaItem { get; set; }
    public Person? Person { get; set; }
}
