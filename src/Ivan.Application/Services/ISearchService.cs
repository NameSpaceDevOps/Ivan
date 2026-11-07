namespace Ivan.Application.Services;

public record SearchResultDto(
    Guid Id,
    string Module,
    string Title,
    string? Subtitle,
    string Url,
    string? Badge = null,
    DateTimeOffset? Date = null);

public interface ISearchService
{
    Task<IReadOnlyList<SearchResultDto>> SearchAsync(string query, CancellationToken ct = default);
    string NormalizePersianText(string text);
}
