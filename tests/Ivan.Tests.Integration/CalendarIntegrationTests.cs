using Ivan.Application.Services;
using Ivan.Domain.Common;
using Ivan.Domain.Enums;
using Xunit;

namespace Ivan.Tests.Integration;

public class CalendarIntegrationTests
{
    private readonly CalendarService _service = new();

    [Fact]
    public void JalaliToUtcAndBack_AcrossEntireYear_IsContinuousAndMonotonic()
    {
        // Test all days of leap year 1403 (366 days)
        DateOnly previousDate = DateOnly.MinValue;

        for (int month = 1; month <= 12; month++)
        {
            int days = JalaliDate.GetDaysInMonth(1403, month);
            for (int day = 1; day <= days; day++)
            {
                var jDate = new JalaliDate(1403, month, day);
                var dateOnly = jDate.ToDateOnly();

                if (previousDate != DateOnly.MinValue)
                {
                    Assert.Equal(previousDate.AddDays(1), dateOnly);
                }

                var roundTrip = JalaliDate.FromDateOnly(dateOnly);
                Assert.Equal(jDate, roundTrip);

                previousDate = dateOnly;
            }
        }
    }

    [Fact]
    public void DateArithmetic_AddMonthsAndYears_HandlesMonthEndAdjustments()
    {
        // 31 Khordad + 2 months -> 31 Mordad
        var endOfSpring = new JalaliDate(1403, 3, 31);
        var inSummer = endOfSpring.AddMonths(2);
        Assert.Equal(new JalaliDate(1403, 5, 31), inSummer);

        // 31 Mordad + 2 months -> 30 Mehr (because Mehr has 30 days)
        var inAutumn = inSummer.AddMonths(2);
        Assert.Equal(new JalaliDate(1403, 7, 30), inAutumn);

        // 30 Esfand 1403 (leap) + 1 year -> 29 Esfand 1404 (non-leap)
        var leapEnd = new JalaliDate(1403, 12, 30);
        var nextYearEnd = leapEnd.AddYears(1);
        Assert.Equal(new JalaliDate(1404, 12, 29), nextYearEnd);
    }
}
