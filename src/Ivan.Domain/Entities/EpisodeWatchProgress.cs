using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class EpisodeWatchProgress : BaseTenantEntity
{
    public Guid EpisodeId { get; set; }
    public Guid PersonId { get; set; }
    public bool IsWatched { get; set; } = true;
    public DateTimeOffset WatchedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public MediaEpisode? Episode { get; set; }
    public Person? Person { get; set; }
}
