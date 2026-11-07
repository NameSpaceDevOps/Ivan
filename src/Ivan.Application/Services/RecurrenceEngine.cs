using System.Text.Json;
using Ivan.Domain.Common;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public class RecurrenceEngine : IRecurrenceEngine
{
    private readonly ICalendarService _calendarService;

    public RecurrenceEngine(ICalendarService calendarService)
    {
        _calendarService = calendarService;
    }

    public DateOnly? GetNextOccurrence(Recurrence recurrence, DateOnly afterDate)
    {
        if (recurrence.EndDate.HasValue && afterDate >= recurrence.EndDate.Value)
            return null;

        var start = recurrence.StartDate;
        var interval = Math.Max(1, recurrence.Interval);
        var cal = recurrence.CalendarMode;

        // If afterDate is before recurrence start date, check start date itself
        if (afterDate < start)
        {
            return start;
        }

        switch (recurrence.Frequency)
        {
            case RecurrenceFrequency.Daily:
            {
                var daysDiff = afterDate.DayNumber - start.DayNumber;
                var step = (daysDiff / interval + 1) * interval;
                var candidate = start.AddDays(step);
                return (recurrence.EndDate.HasValue && candidate > recurrence.EndDate.Value) ? null : candidate;
            }

            case RecurrenceFrequency.Weekly:
            {
                var allowedDays = GetAllowedDaysOfWeek(recurrence.DaysOfWeekJson, start);
                var cursor = afterDate.AddDays(1);

                // Look ahead up to 104 weeks
                for (int i = 0; i < 7 * interval * 52; i++)
                {
                    if (allowedDays.Contains(cursor.DayOfWeek))
                    {
                        var weeksFromStart = (cursor.DayNumber - start.DayNumber) / 7;
                        if (weeksFromStart >= 0 && weeksFromStart % interval == 0)
                        {
                            return (recurrence.EndDate.HasValue && cursor > recurrence.EndDate.Value) ? null : cursor;
                        }
                    }
                    cursor = cursor.AddDays(1);
                }
                return null;
            }

            case RecurrenceFrequency.Monthly:
            {
                if (cal == CalendarType.Jalali)
                {
                    var jStart = _calendarService.ToJalali(start);
                    var jAfter = _calendarService.ToJalali(afterDate);
                    int targetDay = recurrence.DayOfMonth ?? jStart.Day;

                    // Calculate months difference
                    int totalMonthsAfter = (jAfter.Year * 12) + (jAfter.Month - 1);
                    int totalMonthsStart = (jStart.Year * 12) + (jStart.Month - 1);

                    int currentMonthOffset = Math.Max(0, totalMonthsAfter - totalMonthsStart);
                    int nextMultiple = (currentMonthOffset / interval) * interval;

                    // Test this candidate month and subsequent interval months
                    for (int step = nextMultiple; step <= nextMultiple + (interval * 60); step += interval)
                    {
                        var candidateJalaliMonth = jStart.AddMonths(step);
                        int maxDaysInMonth = JalaliDate.GetDaysInMonth(candidateJalaliMonth.Year, candidateJalaliMonth.Month);
                        int day = Math.Min(targetDay, maxDaysInMonth);
                        var candidate = new JalaliDate(candidateJalaliMonth.Year, candidateJalaliMonth.Month, day).ToDateOnly();

                        if (candidate > afterDate)
                        {
                            return (recurrence.EndDate.HasValue && candidate > recurrence.EndDate.Value) ? null : candidate;
                        }
                    }
                    return null;
                }
                else
                {
                    int targetDay = recurrence.DayOfMonth ?? start.Day;
                    var cursor = start;
                    while (cursor <= afterDate)
                    {
                        var nextMonth = cursor.AddMonths(interval);
                        int maxDays = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
                        cursor = new DateOnly(nextMonth.Year, nextMonth.Month, Math.Min(targetDay, maxDays));
                    }
                    return (recurrence.EndDate.HasValue && cursor > recurrence.EndDate.Value) ? null : cursor;
                }
            }

            case RecurrenceFrequency.Yearly:
            {
                if (cal == CalendarType.Jalali)
                {
                    var jStart = _calendarService.ToJalali(start);
                    var jAfter = _calendarService.ToJalali(afterDate);
                    int targetMonth = recurrence.MonthOfYear ?? jStart.Month;
                    int targetDay = recurrence.DayOfMonth ?? jStart.Day;

                    int yearsDiff = Math.Max(0, jAfter.Year - jStart.Year);
                    int nextYearMultiple = (yearsDiff / interval) * interval;

                    for (int step = nextYearMultiple; step <= nextYearMultiple + (interval * 50); step += interval)
                    {
                        int candidateYear = jStart.Year + step;
                        int maxDays = JalaliDate.GetDaysInMonth(candidateYear, targetMonth);
                        int day = Math.Min(targetDay, maxDays);
                        var candidate = new JalaliDate(candidateYear, targetMonth, day).ToDateOnly();

                        if (candidate > afterDate)
                        {
                            return (recurrence.EndDate.HasValue && candidate > recurrence.EndDate.Value) ? null : candidate;
                        }
                    }
                    return null;
                }
                else
                {
                    int targetMonth = recurrence.MonthOfYear ?? start.Month;
                    int targetDay = recurrence.DayOfMonth ?? start.Day;
                    var cursor = start;
                    while (cursor <= afterDate)
                    {
                        var nextYear = cursor.Year + interval;
                        int maxDays = DateTime.DaysInMonth(nextYear, targetMonth);
                        cursor = new DateOnly(nextYear, targetMonth, Math.Min(targetDay, maxDays));
                    }
                    return (recurrence.EndDate.HasValue && cursor > recurrence.EndDate.Value) ? null : cursor;
                }
            }
        }

        return null;
    }

    public IReadOnlyList<DateOnly> GetOccurrences(Recurrence recurrence, DateOnly fromDate, DateOnly toDate, int maxCount = 50)
    {
        var list = new List<DateOnly>();
        var cursor = fromDate.AddDays(-1);

        while (list.Count < maxCount)
        {
            var next = GetNextOccurrence(recurrence, cursor);
            if (!next.HasValue || next.Value > toDate)
                break;

            list.Add(next.Value);
            cursor = next.Value;
        }

        return list;
    }

    private static HashSet<DayOfWeek> GetAllowedDaysOfWeek(string? json, DateOnly defaultStart)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var days = JsonSerializer.Deserialize<int[]>(json);
                if (days != null && days.Length > 0)
                {
                    return days.Select(d => (DayOfWeek)d).ToHashSet();
                }
            }
            catch
            {
                // Fallback to comma-separated
                var parts = json.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var set = new HashSet<DayOfWeek>();
                foreach (var p in parts)
                {
                    if (int.TryParse(p, out int val)) set.Add((DayOfWeek)val);
                }
                if (set.Count > 0) return set;
            }
        }

        return [defaultStart.DayOfWeek];
    }
}
