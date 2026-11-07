using Ivan.Domain.Common;

namespace Ivan.Domain.Entities;

public class MediaEpisode : BaseTenantEntity
{
    public Guid MediaItemId { get; set; }
    public int SeasonNumber { get; set; }
    public int EpisodeNumber { get; set; }
    public string? Title { get; set; }
    public string? Overview { get; set; }
    public DateOnly? AirDate { get; set; }

    // Navigation
    public MediaItem? MediaItem { get; set; }
    public ICollection<EpisodeWatchProgress> WatchProgresses { get; set; } = new List<EpisodeWatchProgress>();
}
