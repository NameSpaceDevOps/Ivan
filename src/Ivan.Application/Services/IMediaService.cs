using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public record MediaItemDto(
    Guid Id,
    string Title,
    string? OriginalTitle,
    MediaType Type,
    string? Director,
    int? ReleaseYear,
    string? PosterUrl,
    string? ImdbId,
    string? Overview,
    int? SeasonCount,
    int? TotalEpisodes,
    int WatchedEpisodesCount,
    MediaEpisodeDto? NextToWatch,
    IReadOnlyList<MediaPersonProgressDto> PersonProgresses);

public record CreateMediaItemRequest(
    string Title,
    MediaType Type = MediaType.Movie,
    string? OriginalTitle = null,
    string? Director = null,
    int? ReleaseYear = null,
    string? PosterUrl = null,
    string? ImdbId = null,
    string? Overview = null,
    int? SeasonCount = null,
    int? TotalEpisodes = null);

public record UpdateMediaItemRequest(
    Guid Id,
    string Title,
    MediaType Type = MediaType.Movie,
    string? OriginalTitle = null,
    string? Director = null,
    int? ReleaseYear = null,
    string? PosterUrl = null,
    string? ImdbId = null,
    string? Overview = null,
    int? SeasonCount = null,
    int? TotalEpisodes = null);

public record MediaPersonProgressDto(
    Guid Id,
    Guid MediaItemId,
    string MediaTitle,
    Guid PersonId,
    string PersonName,
    WatchStatus WatchStatus,
    int? Rating,
    string? Review,
    DateTimeOffset? LastWatchedAt);

public record SetMediaProgressRequest(
    Guid MediaItemId,
    Guid PersonId,
    WatchStatus WatchStatus,
    int? Rating = null,
    string? Review = null);

public record MediaEpisodeDto(
    Guid Id,
    Guid MediaItemId,
    int SeasonNumber,
    int EpisodeNumber,
    string? Title,
    string? Overview,
    DateOnly? AirDate,
    bool IsWatchedByCurrentPerson);

public record CreateEpisodeRequest(
    Guid MediaItemId,
    int SeasonNumber,
    int EpisodeNumber,
    string? Title = null,
    string? Overview = null,
    DateOnly? AirDate = null);

public record GenerateEpisodesRequest(
    Guid MediaItemId,
    int SeasonCount,
    int EpisodesPerSeason);

public record SetEpisodeWatchedRequest(
    Guid EpisodeId,
    Guid PersonId,
    bool IsWatched = true);

public interface IMediaService
{
    // Media Items
    Task<IReadOnlyList<MediaItemDto>> GetMediaItemsAsync(string? search = null, MediaType? type = null, WatchStatus? status = null, Guid? personId = null, CancellationToken ct = default);
    Task<MediaItemDto?> GetMediaItemByIdAsync(Guid id, Guid? personId = null, CancellationToken ct = default);
    Task<MediaItemDto> CreateMediaItemAsync(CreateMediaItemRequest request, CancellationToken ct = default);
    Task<MediaItemDto> UpdateMediaItemAsync(UpdateMediaItemRequest request, CancellationToken ct = default);
    Task DeleteMediaItemAsync(Guid id, CancellationToken ct = default);

    // Watch Progress & Ratings
    Task<MediaPersonProgressDto> SetPersonProgressAsync(SetMediaProgressRequest request, CancellationToken ct = default);

    // Episodes & Seasons
    Task<IReadOnlyList<MediaEpisodeDto>> GetEpisodesAsync(Guid mediaItemId, int? seasonNumber = null, Guid? personId = null, CancellationToken ct = default);
    Task<MediaEpisodeDto?> GetNextToWatchAsync(Guid mediaItemId, Guid personId, CancellationToken ct = default);
    Task<int> GenerateEpisodesAsync(GenerateEpisodesRequest request, CancellationToken ct = default);
    Task SetEpisodeWatchedAsync(SetEpisodeWatchedRequest request, CancellationToken ct = default);
}
