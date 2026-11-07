using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public interface ICalendarService
{
    CalendarType DefaultCalendar { get; }
    DigitStyle DefaultDigitStyle { get; }

    JalaliDate ToJalali(DateOnly date);
    JalaliDate ToJalali(DateTimeOffset dateTimeOffset);
    DateOnly ToDateOnly(int year, int month, int day, CalendarType calendar = CalendarType.Jalali);

    string FormatDate(DateOnly date, string format = "yyyy/MM/dd", CalendarType? calendar = null, DigitStyle? digits = null);
    string FormatDateTime(DateTimeOffset dateTime, string format = "yyyy/MM/dd HH:mm", CalendarType? calendar = null, DigitStyle? digits = null);

    bool TryParseDate(string? input, out DateOnly date, CalendarType? calendar = null);
    DateOnly ParseDate(string input, CalendarType? calendar = null);

    int GetDaysInMonth(int year, int month, CalendarType? calendar = null);
    bool IsLeapYear(int year, CalendarType? calendar = null);

    IReadOnlyList<string> GetMonthNames(CalendarType? calendar = null);
    IReadOnlyList<string> GetShortMonthNames(CalendarType? calendar = null);
    IReadOnlyList<string> GetDayNames(CalendarType? calendar = null, DayOfWeek firstDayOfWeek = DayOfWeek.Saturday);

    string ConvertDigits(string? input, DigitStyle targetStyle);
    string NormalizeDigitsToLatin(string? input);
}
