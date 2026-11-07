using System.Globalization;

namespace Ivan.Domain.Common;

public readonly struct JalaliDate : IEquatable<JalaliDate>, IComparable<JalaliDate>
{
    private static readonly PersianCalendar Calendar = new();

    public int Year { get; }
    public int Month { get; }
    public int Day { get; }

    public JalaliDate(int year, int month, int day)
    {
        if (year < 1 || year > 9999)
            throw new ArgumentOutOfRangeException(nameof(year), "Year must be between 1 and 9999.");
        if (month < 1 || month > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");

        int daysInMonth = GetDaysInMonth(year, month);
        if (day < 1 || day > daysInMonth)
            throw new ArgumentOutOfRangeException(nameof(day), $"Day must be between 1 and {daysInMonth} for month {month} of year {year}.");

        Year = year;
        Month = month;
        Day = day;
    }

    public static JalaliDate FromDateOnly(DateOnly date)
    {
        var dt = date.ToDateTime(TimeOnly.MinValue);
        return new JalaliDate(
            Calendar.GetYear(dt),
            Calendar.GetMonth(dt),
            Calendar.GetDayOfMonth(dt)
        );
    }

    public static JalaliDate FromDateTimeOffset(DateTimeOffset dateTimeOffset)
    {
        var dt = dateTimeOffset.DateTime;
        return new JalaliDate(
            Calendar.GetYear(dt),
            Calendar.GetMonth(dt),
            Calendar.GetDayOfMonth(dt)
        );
    }

    public DateOnly ToDateOnly()
    {
        var dt = Calendar.ToDateTime(Year, Month, Day, 0, 0, 0, 0);
        return DateOnly.FromDateTime(dt);
    }

    public DateTimeOffset ToDateTimeOffset(TimeOnly? time = null, TimeSpan? offset = null)
    {
        var t = time ?? TimeOnly.MinValue;
        var off = offset ?? TimeSpan.Zero;
        var dt = Calendar.ToDateTime(Year, Month, Day, t.Hour, t.Minute, t.Second, t.Millisecond);
        return new DateTimeOffset(dt, off);
    }

    public static int GetDaysInMonth(int year, int month)
    {
        if (month is >= 1 and <= 6)
            return 31;
        if (month is >= 7 and <= 11)
            return 30;
        return IsLeapYear(year) ? 30 : 29;
    }

    public static bool IsLeapYear(int year)
    {
        return Calendar.IsLeapYear(year);
    }

    public JalaliDate AddDays(int days)
    {
        var date = ToDateOnly().AddDays(days);
        return FromDateOnly(date);
    }

    public JalaliDate AddMonths(int months)
    {
        int totalMonths = (Year * 12) + (Month - 1) + months;
        int newYear = totalMonths / 12;
        int newMonth = (totalMonths % 12) + 1;
        int maxDays = GetDaysInMonth(newYear, newMonth);
        int newDay = Math.Min(Day, maxDays);
        return new JalaliDate(newYear, newMonth, newDay);
    }

    public JalaliDate AddYears(int years)
    {
        int newYear = Year + years;
        int maxDays = GetDaysInMonth(newYear, Month);
        int newDay = Math.Min(Day, maxDays);
        return new JalaliDate(newYear, Month, newDay);
    }

    public override string ToString() => $"{Year:D4}/{Month:D2}/{Day:D2}";

    public bool Equals(JalaliDate other) => Year == other.Year && Month == other.Month && Day == other.Day;

    public override bool Equals(object? obj) => obj is JalaliDate other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Year, Month, Day);

    public int CompareTo(JalaliDate other)
    {
        int yearComparison = Year.CompareTo(other.Year);
        if (yearComparison != 0) return yearComparison;
        int monthComparison = Month.CompareTo(other.Month);
        if (monthComparison != 0) return monthComparison;
        return Day.CompareTo(other.Day);
    }

    public static bool operator ==(JalaliDate left, JalaliDate right) => left.Equals(right);
    public static bool operator !=(JalaliDate left, JalaliDate right) => !left.Equals(right);
    public static bool operator <(JalaliDate left, JalaliDate right) => left.CompareTo(right) < 0;
    public static bool operator <=(JalaliDate left, JalaliDate right) => left.CompareTo(right) <= 0;
    public static bool operator >(JalaliDate left, JalaliDate right) => left.CompareTo(right) > 0;
    public static bool operator >=(JalaliDate left, JalaliDate right) => left.CompareTo(right) >= 0;
}
