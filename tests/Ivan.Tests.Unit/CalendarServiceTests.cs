using Ivan.Application.Services;
using Ivan.Domain.Common;
using Ivan.Domain.Enums;
using Xunit;

namespace Ivan.Tests.Unit;

public class CalendarServiceTests
{
    private readonly CalendarService _service = new();

    [Fact]
    public void JalaliDate_RoundTripToDateOnly_IsConsistent()
    {
        // 1403/01/01 is 2024-03-20
        var jDate = new JalaliDate(1403, 1, 1);
        var dateOnly = jDate.ToDateOnly();
        var roundTrip = JalaliDate.FromDateOnly(dateOnly);

        Assert.Equal(jDate, roundTrip);
        Assert.Equal(new DateOnly(2024, 3, 20), dateOnly);
    }

    [Fact]
    public void JalaliDate_LeapYear_1403_Has30DaysInEsfand()
    {
        Assert.True(JalaliDate.IsLeapYear(1403));
        Assert.Equal(30, JalaliDate.GetDaysInMonth(1403, 12));

        var lastDayOf1403 = new JalaliDate(1403, 12, 30);
        var dateOnly = lastDayOf1403.ToDateOnly();
        Assert.Equal(new DateOnly(2025, 3, 20), dateOnly);
    }

    [Fact]
    public void JalaliDate_NonLeapYear_1404_Has29DaysInEsfand()
    {
        Assert.False(JalaliDate.IsLeapYear(1404));
        Assert.Equal(29, JalaliDate.GetDaysInMonth(1404, 12));

        Assert.Throws<ArgumentOutOfRangeException>(() => new JalaliDate(1404, 12, 30));
    }

    [Theory]
    [InlineData("1403/07/15", 1403, 7, 15)]
    [InlineData("۱۴۰۳/۰۷/۱۵", 1403, 7, 15)]
    [InlineData("١٤٠٣/٠٧/١٥", 1403, 7, 15)]
    [InlineData("1403-7-15", 1403, 7, 15)]
    [InlineData("14031230", 1403, 12, 30)] // Leap year last day
    public void TryParseDate_AcceptsVariousDigitStylesAndFormats(string input, int expectedYear, int expectedMonth, int expectedDay)
    {
        bool success = _service.TryParseDate(input, out var date, CalendarType.Jalali);
        Assert.True(success);

        var jDate = _service.ToJalali(date);
        Assert.Equal(expectedYear, jDate.Year);
        Assert.Equal(expectedMonth, jDate.Month);
        Assert.Equal(expectedDay, jDate.Day);
    }

    [Fact]
    public void TryParseDate_InvalidDayInNonLeapYear_ReturnsFalse()
    {
        // 1404 is not a leap year, 30 Esfand is invalid
        bool success = _service.TryParseDate("1404/12/30", out _, CalendarType.Jalali);
        Assert.False(success);
    }

    [Theory]
    [InlineData("1234567890", DigitStyle.Persian, "۱۲۳۴۵۶۷۸۹۰")]
    [InlineData("۱۲۳۴۵۶۷۸۹۰", DigitStyle.Latin, "1234567890")]
    [InlineData("1234567890", DigitStyle.ArabicIndic, "١٢٣٤٥٦٧٨٩٠")]
    [InlineData("قیمت: 12500 تومان", DigitStyle.Persian, "قیمت: ۱۲۵۰۰ تومان")]
    public void ConvertDigits_TransformsDigitsCorrectly(string input, DigitStyle style, string expected)
    {
        var result = _service.ConvertDigits(input, style);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatDate_WithPersianDigitsAndMonthNames()
    {
        var date = new DateOnly(2024, 3, 20); // 1403/01/01
        var formatted = _service.FormatDate(date, "d MMMM yyyy", CalendarType.Jalali, DigitStyle.Persian);
        Assert.Equal("۱ فروردین ۱۴۰۳", formatted);
    }
}
