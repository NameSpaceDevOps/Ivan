using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class MediaItem : BaseTenantEntity
{
    public string Title { get; set; } = string.Empty;
    public string? OriginalTitle { get; set; }
    public MediaType Type { get; set; } = MediaType.Movie;
    public string? Director { get; set; }
    public int? ReleaseYear { get; set; }
    public string? PosterUrl { get; set; }
    public string? ImdbId { get; set; }
    public string? Overview { get; set; }
    public int? SeasonCount { get; set; }
    public int? TotalEpisodes { get; set; }

    // Navigation
    public ICollection<MediaPersonProgress> Progresses { get; set; } = new List<MediaPersonProgress>();
    public ICollection<MediaEpisode> Episodes { get; set; } = new List<MediaEpisode>();
}
