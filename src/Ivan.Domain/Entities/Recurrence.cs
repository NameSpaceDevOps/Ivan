using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class Recurrence : BaseTenantEntity
{
    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Monthly;
    public int Interval { get; set; } = 1; // e.g. every 1 month, every 2 weeks
    public CalendarType CalendarMode { get; set; } = CalendarType.Jalali;

    // Optional specific days for weekly recurrence: comma-separated DayOfWeek integers, e.g. "6,2" for Saturday, Tuesday
    public string? DaysOfWeekJson { get; set; }

    // Optional specific day of month (1-31)
    public int? DayOfMonth { get; set; }

    // Optional specific month of year (1-12)
    public int? MonthOfYear { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public TimeOnly? TimeOfDay { get; set; }
    public string TimeZone { get; set; } = "Asia/Tehran";
}
