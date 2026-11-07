using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Xunit;

namespace Ivan.Tests.Unit;

public class RecurrenceEngineTests
{
    private readonly CalendarService _calendarService = new();
    private readonly RecurrenceEngine _engine;

    public RecurrenceEngineTests()
    {
        _engine = new RecurrenceEngine(_calendarService);
    }

    [Fact]
    public void DailyRecurrence_CalculatesCorrectStep()
    {
        var start = new DateOnly(2024, 3, 20); // 1403/01/01
        var rec = new Recurrence
        {
            Frequency = RecurrenceFrequency.Daily,
            Interval = 3,
            StartDate = start
        };

        var next1 = _engine.GetNextOccurrence(rec, start);
        Assert.Equal(start.AddDays(3), next1);

        var next2 = _engine.GetNextOccurrence(rec, start.AddDays(4));
        Assert.Equal(start.AddDays(6), next2);
    }

    [Fact]
    public void JalaliMonthlyRecurrence_ClampsToMonthEnd_For30DayMonths()
    {
        // 31 Shahrivar 1403 (1403/06/31)
        var start = _calendarService.ToDateOnly(1403, 6, 31, CalendarType.Jalali);
        var rec = new Recurrence
        {
            Frequency = RecurrenceFrequency.Monthly,
            Interval = 1,
            CalendarMode = CalendarType.Jalali,
            DayOfMonth = 31,
            StartDate = start
        };

        // Next occurrence should be in Mehr (Month 7), which has only 30 days -> 1403/07/30
        var next = _engine.GetNextOccurrence(rec, start);
        Assert.NotNull(next);

        var jNext = _calendarService.ToJalali(next.Value);
        Assert.Equal(1403, jNext.Year);
        Assert.Equal(7, jNext.Month);
        Assert.Equal(30, jNext.Day);
    }

    [Fact]
    public void JalaliMonthlyRecurrence_LeapYear1403_Preserves30thOfEsfand()
    {
        // 30 Bahman 1403 (1403/11/30)
        var start = _calendarService.ToDateOnly(1403, 11, 30, CalendarType.Jalali);
        var rec = new Recurrence
        {
            Frequency = RecurrenceFrequency.Monthly,
            Interval = 1,
            CalendarMode = CalendarType.Jalali,
            DayOfMonth = 30,
            StartDate = start
        };

        // Next month is Esfand 1403 (leap year) -> 1403/12/30
        var next = _engine.GetNextOccurrence(rec, start);
        Assert.NotNull(next);

        var jNext = _calendarService.ToJalali(next.Value);
        Assert.Equal(1403, jNext.Year);
        Assert.Equal(12, jNext.Month);
        Assert.Equal(30, jNext.Day);
    }

    [Fact]
    public void JalaliYearlyRecurrence_30Esfand_ClampsTo29InCommonYear()
    {
        // 30 Esfand 1403 (leap year)
        var start = _calendarService.ToDateOnly(1403, 12, 30, CalendarType.Jalali);
        var rec = new Recurrence
        {
            Frequency = RecurrenceFrequency.Yearly,
            Interval = 1,
            CalendarMode = CalendarType.Jalali,
            MonthOfYear = 12,
            DayOfMonth = 30,
            StartDate = start
        };

        // 1 year later is 1404 (non-leap year, 29 days in Esfand) -> 1404/12/29
        var next = _engine.GetNextOccurrence(rec, start);
        Assert.NotNull(next);

        var jNext = _calendarService.ToJalali(next.Value);
        Assert.Equal(1404, jNext.Year);
        Assert.Equal(12, jNext.Month);
        Assert.Equal(29, jNext.Day);
    }

    [Fact]
    public void WeeklyRecurrence_FiltersBySpecifiedDays()
    {
        // Saturday is DayOfWeek.Saturday (6), Tuesday is DayOfWeek.Tuesday (2)
        var start = new DateOnly(2024, 3, 23); // Saturday
        Assert.Equal(DayOfWeek.Saturday, start.DayOfWeek);

        var rec = new Recurrence
        {
            Frequency = RecurrenceFrequency.Weekly,
            Interval = 1,
            DaysOfWeekJson = "[6, 2]", // Saturday and Tuesday
            StartDate = start
        };

        // Next after Saturday should be Tuesday (3 days later)
        var next = _engine.GetNextOccurrence(rec, start);
        Assert.NotNull(next);
        Assert.Equal(DayOfWeek.Tuesday, next.Value.DayOfWeek);
        Assert.Equal(start.AddDays(3), next.Value);
    }
}
