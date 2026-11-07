namespace Ivan.Application.Services;

public enum CalendarItemType
{
    Task = 1,
    Maintenance = 2,
    ImportantDate = 3,
    Birthday = 4,
    HealthDue = 5,
    DocumentExpiry = 6,
    WarrantyExpiry = 7,
    ChequeDue = 8,
    InstallmentDue = 9
}

public record CalendarEventDto(
    Guid Id,
    CalendarItemType Type,
    string Title,
    DateTimeOffset DueAt,
    string? Description = null,
    string? Url = null,
    string? BadgeColor = null);

public record CalendarViewRequest(
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    Guid? PersonId = null,
    Guid? PropertyId = null);

public interface ICalendarViewService
{
    Task<List<CalendarEventDto>> GetCalendarEventsAsync(CalendarViewRequest request, CancellationToken ct = default);
}
