using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class MediaService : IMediaService
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;
    private readonly IAuditService _auditService;

    public MediaService(
        IIvanDbContext db,
        ICurrentUserContext userContext,
        IAuditService auditService)
    {
        _db = db;
        _userContext = userContext;
        _auditService = auditService;
    }

    public async Task<IReadOnlyList<MediaItemDto>> GetMediaItemsAsync(string? search = null, MediaType? type = null, WatchStatus? status = null, Guid? personId = null, CancellationToken ct = default)
    {
        var query = _db.MediaItems
            .Include(m => m.Progresses)
                .ThenInclude(p => p.Person)
            .Include(m => m.Episodes)
                .ThenInclude(e => e.WatchProgresses)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(m => m.Title.Contains(s) || (m.OriginalTitle != null && m.OriginalTitle.Contains(s)) || (m.Director != null && m.Director.Contains(s)));
        }

        if (type.HasValue)
            query = query.Where(m => m.Type == type.Value);

        if (status.HasValue)
        {
            if (personId.HasValue)
                query = query.Where(m => m.Progresses.Any(p => p.PersonId == personId.Value && p.WatchStatus == status.Value));
            else
                query = query.Where(m => m.Progresses.Any(p => p.WatchStatus == status.Value));
        }

        var list = await query.ToListAsync(ct);

        return list.OrderBy(m => m.Title).Select(m => MapToDto(m, personId)).ToList();
    }

    public async Task<MediaItemDto?> GetMediaItemByIdAsync(Guid id, Guid? personId = null, CancellationToken ct = default)
    {
        var item = await _db.MediaItems
            .Include(m => m.Progresses)
                .ThenInclude(p => p.Person)
            .Include(m => m.Episodes)
                .ThenInclude(e => e.WatchProgresses)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        return item == null ? null : MapToDto(item, personId);
    }

    public async Task<MediaItemDto> CreateMediaItemAsync(CreateMediaItemRequest request, CancellationToken ct = default)
    {
        var item = new MediaItem
        {
            HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
            Title = request.Title,
            OriginalTitle = request.OriginalTitle,
            Type = request.Type,
            Director = request.Director,
            ReleaseYear = request.ReleaseYear,
            PosterUrl = request.PosterUrl,
            ImdbId = request.ImdbId,
            Overview = request.Overview,
            SeasonCount = request.SeasonCount,
            TotalEpisodes = request.TotalEpisodes
        };

        _db.MediaItems.Add(item);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateMediaItem", "MediaItem", item.Id.ToString(), $"Created media {item.Title} ({item.Type})", ct);

        return (await GetMediaItemByIdAsync(item.Id, null, ct))!;
    }

    public async Task<MediaItemDto> UpdateMediaItemAsync(UpdateMediaItemRequest request, CancellationToken ct = default)
    {
        var item = await _db.MediaItems.FirstOrDefaultAsync(m => m.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Media item {request.Id} not found.");

        item.Title = request.Title;
        item.OriginalTitle = request.OriginalTitle;
        item.Type = request.Type;
        item.Director = request.Director;
        item.ReleaseYear = request.ReleaseYear;
        item.PosterUrl = request.PosterUrl;
        item.ImdbId = request.ImdbId;
        item.Overview = request.Overview;
        item.SeasonCount = request.SeasonCount;
        item.TotalEpisodes = request.TotalEpisodes;

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("UpdateMediaItem", "MediaItem", item.Id.ToString(), $"Updated media {item.Title}", ct);

        return (await GetMediaItemByIdAsync(item.Id, null, ct))!;
    }

    public async Task DeleteMediaItemAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _db.MediaItems.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new KeyNotFoundException($"Media item {id} not found.");

        _db.MediaItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("DeleteMediaItem", "MediaItem", id.ToString(), $"Deleted media {item.Title}", ct);
    }

    public async Task<MediaPersonProgressDto> SetPersonProgressAsync(SetMediaProgressRequest request, CancellationToken ct = default)
    {
        var progress = await _db.MediaPersonProgresses
            .Include(p => p.MediaItem)
            .Include(p => p.Person)
            .FirstOrDefaultAsync(p => p.MediaItemId == request.MediaItemId && p.PersonId == request.PersonId, ct);

        if (progress == null)
        {
            progress = new MediaPersonProgress
            {
                HouseholdId = _userContext.HouseholdId ?? Guid.Empty,
                MediaItemId = request.MediaItemId,
                PersonId = request.PersonId,
                WatchStatus = request.WatchStatus,
                Rating = request.Rating,
                Review = request.Review,
                LastWatchedAt = DateTimeOffset.UtcNow
            };
            _db.MediaPersonProgresses.Add(progress);
        }
        else
        {
            progress.WatchStatus = request.WatchStatus;
            if (request.Rating.HasValue) progress.Rating = request.Rating;
            if (request.Review != null) progress.Review = request.Review;
            progress.LastWatchedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("SetPersonProgress", "MediaPersonProgress", progress.Id.ToString(), $"Updated watch status to {request.WatchStatus} for media {request.MediaItemId}", ct);

        var media = await _db.MediaItems.FindAsync([progress.MediaItemId], ct);
        var person = await _db.People.FindAsync([progress.PersonId], ct);

        return new MediaPersonProgressDto(
            progress.Id,
            progress.MediaItemId,
            media?.Title ?? "",
            progress.PersonId,
            person?.DisplayName ?? "",
            progress.WatchStatus,
            progress.Rating,
            progress.Review,
            progress.LastWatchedAt);
    }

    public async Task<IReadOnlyList<MediaEpisodeDto>> GetEpisodesAsync(Guid mediaItemId, int? seasonNumber = null, Guid? personId = null, CancellationToken ct = default)
    {
        var query = _db.MediaEpisodes
            .Include(e => e.WatchProgresses)
            .Where(e => e.MediaItemId == mediaItemId);

        if (seasonNumber.HasValue)
            query = query.Where(e => e.SeasonNumber == seasonNumber.Value);

        var list = await query
            .OrderBy(e => e.SeasonNumber)
            .ThenBy(e => e.EpisodeNumber)
            .ToListAsync(ct);

        return list.Select(e => new MediaEpisodeDto(
            e.Id,
            e.MediaItemId,
            e.SeasonNumber,
            e.EpisodeNumber,
            e.Title,
            e.Overview,
            e.AirDate,
            personId.HasValue && e.WatchProgresses.Any(wp => wp.PersonId == personId.Value && wp.IsWatched))).ToList();
    }

    public async Task<MediaEpisodeDto?> GetNextToWatchAsync(Guid mediaItemId, Guid personId, CancellationToken ct = default)
    {
        var episodes = await _db.MediaEpisodes
            .Include(e => e.WatchProgresses)
            .Where(e => e.MediaItemId == mediaItemId)
            .OrderBy(e => e.SeasonNumber)
            .ThenBy(e => e.EpisodeNumber)
            .ToListAsync(ct);

        if (!episodes.Any()) return null;

        var firstUnwatched = episodes.FirstOrDefault(e =>
            !e.WatchProgresses.Any(wp => wp.PersonId == personId && wp.IsWatched));

        if (firstUnwatched == null) return null;

        return new MediaEpisodeDto(
            firstUnwatched.Id,
            firstUnwatched.MediaItemId,
            firstUnwatched.SeasonNumber,
            firstUnwatched.EpisodeNumber,
            firstUnwatched.Title,
            firstUnwatched.Overview,
            firstUnwatched.AirDate,
            false);
    }

    public async Task<int> GenerateEpisodesAsync(GenerateEpisodesRequest request, CancellationToken ct = default)
    {
        var media = await _db.MediaItems
            .Include(m => m.Episodes)
            .FirstOrDefaultAsync(m => m.Id == request.MediaItemId, ct)
            ?? throw new KeyNotFoundException($"Media item {request.MediaItemId} not found.");

        var existingEpisodes = media.Episodes
            .ToLookup(e => (e.SeasonNumber, e.EpisodeNumber));

        var toAdd = new List<MediaEpisode>();

        for (int s = 1; s <= request.SeasonCount; s++)
        {
            for (int ep = 1; ep <= request.EpisodesPerSeason; ep++)
            {
                if (!existingEpisodes.Contains((s, ep)))
                {
                    toAdd.Add(new MediaEpisode
                    {
                        HouseholdId = media.HouseholdId,
                        MediaItemId = media.Id,
                        SeasonNumber = s,
                        EpisodeNumber = ep,
                        Title = $"Episode {ep}"
                    });
                }
            }
        }

        if (toAdd.Any())
        {
            _db.MediaEpisodes.AddRange(toAdd);
            media.SeasonCount = Math.Max(media.SeasonCount ?? 0, request.SeasonCount);
            media.TotalEpisodes = (media.TotalEpisodes ?? 0) + toAdd.Count;
            await _db.SaveChangesAsync(ct);
            await _auditService.LogAsync("GenerateEpisodes", "MediaEpisode", media.Id.ToString(), $"Generated {toAdd.Count} episodes for {media.Title}", ct);
        }

        return toAdd.Count;
    }

    public async Task SetEpisodeWatchedAsync(SetEpisodeWatchedRequest request, CancellationToken ct = default)
    {
        var episode = await _db.MediaEpisodes
            .Include(e => e.MediaItem)
            .FirstOrDefaultAsync(e => e.Id == request.EpisodeId, ct)
            ?? throw new KeyNotFoundException($"Episode {request.EpisodeId} not found.");

        var progress = await _db.EpisodeWatchProgresses
            .FirstOrDefaultAsync(wp => wp.EpisodeId == request.EpisodeId && wp.PersonId == request.PersonId, ct);

        if (progress == null)
        {
            progress = new EpisodeWatchProgress
            {
                HouseholdId = episode.HouseholdId,
                EpisodeId = request.EpisodeId,
                PersonId = request.PersonId,
                IsWatched = request.IsWatched,
                WatchedAt = DateTimeOffset.UtcNow
            };
            _db.EpisodeWatchProgresses.Add(progress);
        }
        else
        {
            progress.IsWatched = request.IsWatched;
            progress.WatchedAt = DateTimeOffset.UtcNow;
        }

        // Update overall media progress if currently PlanToWatch
        var mediaProgress = await _db.MediaPersonProgresses
            .FirstOrDefaultAsync(mp => mp.MediaItemId == episode.MediaItemId && mp.PersonId == request.PersonId, ct);

        if (mediaProgress == null && request.IsWatched)
        {
            _db.MediaPersonProgresses.Add(new MediaPersonProgress
            {
                HouseholdId = episode.HouseholdId,
                MediaItemId = episode.MediaItemId,
                PersonId = request.PersonId,
                WatchStatus = WatchStatus.Watching,
                LastWatchedAt = DateTimeOffset.UtcNow
            });
        }
        else if (mediaProgress != null && mediaProgress.WatchStatus == WatchStatus.PlanToWatch && request.IsWatched)
        {
            mediaProgress.WatchStatus = WatchStatus.Watching;
            mediaProgress.LastWatchedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("SetEpisodeWatched", "EpisodeWatchProgress", request.EpisodeId.ToString(), $"Set watched={request.IsWatched} for episode {episode.SeasonNumber}x{episode.EpisodeNumber}", ct);
    }

    private static MediaItemDto MapToDto(MediaItem m, Guid? personId)
    {
        var progresses = m.Progresses.Select(p => new MediaPersonProgressDto(
            p.Id,
            p.MediaItemId,
            m.Title,
            p.PersonId,
            p.Person?.DisplayName ?? "",
            p.WatchStatus,
            p.Rating,
            p.Review,
            p.LastWatchedAt)).ToList();

        int watchedCount = 0;
        MediaEpisodeDto? nextToWatch = null;

        if (personId.HasValue && m.Episodes.Any())
        {
            var orderedEpisodes = m.Episodes.OrderBy(e => e.SeasonNumber).ThenBy(e => e.EpisodeNumber).ToList();
            watchedCount = orderedEpisodes.Count(e => e.WatchProgresses.Any(wp => wp.PersonId == personId.Value && wp.IsWatched));

            var nextEp = orderedEpisodes.FirstOrDefault(e => !e.WatchProgresses.Any(wp => wp.PersonId == personId.Value && wp.IsWatched));
            if (nextEp != null)
            {
                nextToWatch = new MediaEpisodeDto(
                    nextEp.Id,
                    nextEp.MediaItemId,
                    nextEp.SeasonNumber,
                    nextEp.EpisodeNumber,
                    nextEp.Title,
                    nextEp.Overview,
                    nextEp.AirDate,
                    false);
            }
        }

        return new MediaItemDto(
            m.Id,
            m.Title,
            m.OriginalTitle,
            m.Type,
            m.Director,
            m.ReleaseYear,
            m.PosterUrl,
            m.ImdbId,
            m.Overview,
            m.SeasonCount,
            m.TotalEpisodes,
            watchedCount,
            nextToWatch,
            progresses);
    }
}
