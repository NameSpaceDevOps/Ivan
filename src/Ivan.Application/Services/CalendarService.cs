using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public class CalendarService : ICalendarService
{
    private static readonly PersianCalendar PersianCal = new();
    private static readonly GregorianCalendar GregorianCal = new();

    private static readonly string[] JalaliMonths =
    [
        "فروردین", "اردیبهشت", "خرداد",
        "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر",
        "دی", "بهمن", "اسفند"
    ];

    private static readonly string[] PersianDayNamesStartingSaturday =
    [
        "شنبه", "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنج‌شنبه", "جمعه"
    ];

    private static readonly string[] PersianDayShortNamesStartingSaturday =
    [
        "ش", "ی", "د", "س", "چ", "پ", "ج"
    ];

    public CalendarType DefaultCalendar { get; set; } = CalendarType.Jalali;
    public DigitStyle DefaultDigitStyle { get; set; } = DigitStyle.Persian;

    public JalaliDate ToJalali(DateOnly date) => JalaliDate.FromDateOnly(date);

    public JalaliDate ToJalali(DateTimeOffset dateTimeOffset) => JalaliDate.FromDateTimeOffset(dateTimeOffset);

    public DateOnly ToDateOnly(int year, int month, int day, CalendarType calendar = CalendarType.Jalali)
    {
        if (calendar == CalendarType.Jalali)
        {
            var jDate = new JalaliDate(year, month, day);
            return jDate.ToDateOnly();
        }

        return new DateOnly(year, month, day);
    }

    public string FormatDate(DateOnly date, string format = "yyyy/MM/dd", CalendarType? calendar = null, DigitStyle? digits = null)
    {
        var cal = calendar ?? DefaultCalendar;
        var dig = digits ?? DefaultDigitStyle;

        string formatted;
        if (cal == CalendarType.Jalali)
        {
            var jDate = ToJalali(date);
            var dt = date.ToDateTime(TimeOnly.MinValue);
            var dayOfWeekIndex = ((int)dt.DayOfWeek + 1) % 7; // Saturday = 0, Friday = 6

            formatted = format
                .Replace("yyyy", jDate.Year.ToString("D4"))
                .Replace("yy", (jDate.Year % 100).ToString("D2"))
                .Replace("MMMM", JalaliMonths[jDate.Month - 1])
                .Replace("MM", jDate.Month.ToString("D2"))
                .Replace("M", jDate.Month.ToString())
                .Replace("dddd", PersianDayNamesStartingSaturday[dayOfWeekIndex])
                .Replace("ddd", PersianDayShortNamesStartingSaturday[dayOfWeekIndex])
                .Replace("dd", jDate.Day.ToString("D2"))
                .Replace("d", jDate.Day.ToString());
        }
        else
        {
            formatted = date.ToString(format, CultureInfo.InvariantCulture);
        }

        return ConvertDigits(formatted, dig);
    }

    public string FormatDateTime(DateTimeOffset dateTime, string format = "yyyy/MM/dd HH:mm", CalendarType? calendar = null, DigitStyle? digits = null)
    {
        var cal = calendar ?? DefaultCalendar;
        var dig = digits ?? DefaultDigitStyle;

        var dateOnly = DateOnly.FromDateTime(dateTime.DateTime);
        var timeStr = dateTime.ToString("HH:mm:ss");

        var formattedDate = FormatDate(dateOnly, format, cal, DigitStyle.Latin);
        var formatted = formattedDate
            .Replace("HH:mm:ss", timeStr)
            .Replace("HH:mm", dateTime.ToString("HH:mm"))
            .Replace("HH", dateTime.ToString("HH"))
            .Replace("mm", dateTime.ToString("mm"))
            .Replace("ss", dateTime.ToString("ss"));

        return ConvertDigits(formatted, dig);
    }

    public bool TryParseDate(string? input, out DateOnly date, CalendarType? calendar = null)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var normalized = NormalizeDigitsToLatin(input.Trim());
        var cal = calendar ?? DefaultCalendar;

        // Match patterns like: yyyy/MM/dd, yyyy-MM-dd, yyyy.MM.dd or yyyyMMdd
        var match = Regex.Match(normalized, @"^(\d{4})[\/\-\.](\d{1,2})[\/\-\.](\d{1,2})$");
        if (!match.Success)
        {
            var compactMatch = Regex.Match(normalized, @"^(\d{4})(\d{2})(\d{2})$");
            if (!compactMatch.Success)
            {
                return false;
            }
            match = compactMatch;
        }

        if (!int.TryParse(match.Groups[1].Value, out int year) ||
            !int.TryParse(match.Groups[2].Value, out int month) ||
            !int.TryParse(match.Groups[3].Value, out int day))
        {
            return false;
        }

        try
        {
            if (cal == CalendarType.Jalali)
            {
                if (month < 1 || month > 12) return false;
                int maxDays = GetDaysInMonth(year, month, CalendarType.Jalali);
                if (day < 1 || day > maxDays) return false;

                var jDate = new JalaliDate(year, month, day);
                date = jDate.ToDateOnly();
                return true;
            }
            else
            {
                if (month < 1 || month > 12) return false;
                int maxDays = DateTime.DaysInMonth(year, month);
                if (day < 1 || day > maxDays) return false;

                date = new DateOnly(year, month, day);
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    public DateOnly ParseDate(string input, CalendarType? calendar = null)
    {
        if (!TryParseDate(input, out var date, calendar))
            throw new FormatException($"String '{input}' was not recognized as a valid date for {calendar ?? DefaultCalendar}.");
        return date;
    }

    public int GetDaysInMonth(int year, int month, CalendarType? calendar = null)
    {
        var cal = calendar ?? DefaultCalendar;
        if (cal == CalendarType.Jalali)
        {
            return JalaliDate.GetDaysInMonth(year, month);
        }
        return DateTime.DaysInMonth(year, month);
    }

    public bool IsLeapYear(int year, CalendarType? calendar = null)
    {
        var cal = calendar ?? DefaultCalendar;
        if (cal == CalendarType.Jalali)
        {
            return PersianCal.IsLeapYear(year);
        }
        return DateTime.IsLeapYear(year);
    }

    public IReadOnlyList<string> GetMonthNames(CalendarType? calendar = null)
    {
        var cal = calendar ?? DefaultCalendar;
        if (cal == CalendarType.Jalali)
        {
            return JalaliMonths;
        }
        return CultureInfo.InvariantCulture.DateTimeFormat.MonthNames
            .Where(m => !string.IsNullOrEmpty(m))
            .ToArray();
    }

    public IReadOnlyList<string> GetShortMonthNames(CalendarType? calendar = null)
    {
        return GetMonthNames(calendar);
    }

    public IReadOnlyList<string> GetDayNames(CalendarType? calendar = null, DayOfWeek firstDayOfWeek = DayOfWeek.Saturday)
    {
        var cal = calendar ?? DefaultCalendar;
        if (cal == CalendarType.Jalali)
        {
            if (firstDayOfWeek == DayOfWeek.Saturday)
            {
                return PersianDayNamesStartingSaturday;
            }
        }

        var list = new List<string>();
        for (int i = 0; i < 7; i++)
        {
            var dow = (DayOfWeek)(((int)firstDayOfWeek + i) % 7);
            list.Add(dow.ToString());
        }
        return list;
    }

    public string ConvertDigits(string? input, DigitStyle targetStyle)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            if (c >= '0' && c <= '9')
            {
                int digit = c - '0';
                sb.Append(targetStyle switch
                {
                    DigitStyle.Persian => (char)('۰' + digit),
                    DigitStyle.ArabicIndic => (char)('٠' + digit),
                    _ => c
                });
            }
            else if (c >= '۰' && c <= '۹')
            {
                int digit = c - '۰';
                sb.Append(targetStyle switch
                {
                    DigitStyle.Persian => c,
                    DigitStyle.ArabicIndic => (char)('٠' + digit),
                    _ => (char)('0' + digit)
                });
            }
            else if (c >= '٠' && c <= '٩')
            {
                int digit = c - '٠';
                sb.Append(targetStyle switch
                {
                    DigitStyle.Persian => (char)('۰' + digit),
                    DigitStyle.ArabicIndic => c,
                    _ => (char)('0' + digit)
                });
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    public string NormalizeDigitsToLatin(string? input)
    {
        return ConvertDigits(input, DigitStyle.Latin);
    }
}
