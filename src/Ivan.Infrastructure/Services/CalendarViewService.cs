using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class CalendarViewService : ICalendarViewService
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;

    public CalendarViewService(
        IIvanDbContext db,
        ICurrentUserContext userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    public async Task<List<CalendarEventDto>> GetCalendarEventsAsync(CalendarViewRequest request, CancellationToken ct = default)
    {
        var events = new List<CalendarEventDto>();
        var startUtc = request.StartDate;
        var endUtc = request.EndDate;
        var startDateOnly = DateOnly.FromDateTime(startUtc.UtcDateTime);
        var endDateOnly = DateOnly.FromDateTime(endUtc.UtcDateTime);

        // 1. Tasks
        var taskQuery = _db.TaskItems.Where(t => t.DueAt.HasValue && t.Status != TaskItemStatus.Done && t.Status != TaskItemStatus.Cancelled);
        if (request.PersonId.HasValue)
            taskQuery = taskQuery.Where(t => t.AssigneePersonId == request.PersonId.Value);
        if (request.PropertyId.HasValue)
            taskQuery = taskQuery.Where(t => t.PropertyId == request.PropertyId.Value);

        var tasks = await taskQuery.ToListAsync(ct);
        foreach (var task in tasks)
        {
            if (task.DueAt.HasValue && task.DueAt.Value >= startUtc && task.DueAt.Value <= endUtc)
            {
                events.Add(new CalendarEventDto(
                    task.Id,
                    CalendarItemType.Task,
                    $"Task: {task.Title}",
                    task.DueAt.Value,
                    task.Details,
                    $"/tasks/{task.Id}",
                    task.Priority == TaskPriority.Urgent ? "var(--rz-danger)" : "var(--rz-info)"));
            }
        }

        // 2. Maintenance Plans
        var maintQuery = _db.MaintenancePlans
            .Include(m => m.Asset)
            .Where(m => m.NextDueAt.HasValue);

        if (request.PropertyId.HasValue)
            maintQuery = maintQuery.Where(m => m.Asset != null && m.Asset.PropertyId == request.PropertyId.Value);

        var maints = await maintQuery.ToListAsync(ct);
        foreach (var m in maints)
        {
            if (m.NextDueAt.HasValue && m.NextDueAt.Value >= startUtc && m.NextDueAt.Value <= endUtc)
            {
                events.Add(new CalendarEventDto(
                    m.Id,
                    CalendarItemType.Maintenance,
                    $"Maintenance: {m.Title} ({m.Asset?.Name})",
                    m.NextDueAt.Value,
                    m.EstimatedCost.HasValue ? $"Est: {m.EstimatedCost:N0}" : null,
                    $"/assets/{m.AssetId}",
                    "var(--rz-warning)"));
            }
        }

        // 3. Important Dates
        var impQuery = _db.ImportantDates.AsQueryable();
        if (request.PersonId.HasValue)
            impQuery = impQuery.Where(d => d.PersonId == request.PersonId.Value);

        var impDates = await impQuery.ToListAsync(ct);
        var startYear = startDateOnly.Year;
        var endYear = endDateOnly.Year;

        foreach (var imp in impDates)
        {
            if (imp.RepeatsYearly)
            {
                for (int y = startYear; y <= endYear; y++)
                {
                    // Safe day clamp for leap years/month lengths
                    var daysInMonth = DateTime.DaysInMonth(y, imp.Date.Month);
                    var day = Math.Min(imp.Date.Day, daysInMonth);
                    var occurrenceDate = new DateOnly(y, imp.Date.Month, day);
                    if (occurrenceDate >= startDateOnly && occurrenceDate <= endDateOnly)
                    {
                        var due = new DateTimeOffset(occurrenceDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
                        events.Add(new CalendarEventDto(
                            imp.Id,
                            CalendarItemType.ImportantDate,
                            imp.Title,
                            due,
                            null,
                            null,
                            "var(--rz-success)"));
                    }
                }
            }
            else if (imp.Date >= startDateOnly && imp.Date <= endDateOnly)
            {
                var due = new DateTimeOffset(imp.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
                events.Add(new CalendarEventDto(
                    imp.Id,
                    CalendarItemType.ImportantDate,
                    imp.Title,
                    due,
                    null,
                    null,
                    "var(--rz-success)"));
            }
        }

        // 4. Health Records Next Due Dates
        var healthQuery = _db.HealthRecords
            .Include(h => h.Person)
            .Where(h => h.NextDueDate.HasValue);

        if (request.PersonId.HasValue)
            healthQuery = healthQuery.Where(h => h.PersonId == request.PersonId.Value);

        var healthList = await healthQuery.ToListAsync(ct);
        foreach (var h in healthList)
        {
            if (h.NextDueDate.HasValue && h.NextDueDate.Value >= startDateOnly && h.NextDueDate.Value <= endDateOnly)
            {
                var due = new DateTimeOffset(h.NextDueDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
                events.Add(new CalendarEventDto(
                    h.Id,
                    CalendarItemType.HealthDue,
                    $"Health: {h.Title} ({h.Person?.DisplayName})",
                    due,
                    h.DetailsJson,
                    null,
                    "var(--rz-secondary)"));
            }
        }

        // 5. Document Expiries
        var docQuery = _db.PersonDocuments
            .Include(d => d.Person)
            .Where(d => d.ExpiryDate.HasValue);

        if (request.PersonId.HasValue)
            docQuery = docQuery.Where(d => d.PersonId == request.PersonId.Value);

        var docList = await docQuery.ToListAsync(ct);
        foreach (var d in docList)
        {
            if (d.ExpiryDate.HasValue && d.ExpiryDate.Value >= startDateOnly && d.ExpiryDate.Value <= endDateOnly)
            {
                var due = new DateTimeOffset(d.ExpiryDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
                events.Add(new CalendarEventDto(
                    d.Id,
                    CalendarItemType.DocumentExpiry,
                    $"Doc Expiry: {d.Type} ({d.Person?.DisplayName})",
                    due,
                    d.Number,
                    null,
                    "var(--rz-danger)"));
            }
        }

        // 6. Asset Warranty Expiries
        var assetQuery = _db.Assets.Where(a => a.WarrantyEnd.HasValue);
        if (request.PropertyId.HasValue)
            assetQuery = assetQuery.Where(a => a.PropertyId == request.PropertyId.Value);

        var assetList = await assetQuery.ToListAsync(ct);
        foreach (var a in assetList)
        {
            if (a.WarrantyEnd.HasValue && a.WarrantyEnd.Value >= startDateOnly && a.WarrantyEnd.Value <= endDateOnly)
            {
                var due = new DateTimeOffset(a.WarrantyEnd.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
                events.Add(new CalendarEventDto(
                    a.Id,
                    CalendarItemType.WarrantyExpiry,
                    $"Warranty Expiring: {a.Name}",
                    due,
                    a.Brand,
                    $"/assets/{a.Id}",
                    "var(--rz-warning)"));
            }
        }

        return events.OrderBy(e => e.DueAt).ToList();
    }
}
