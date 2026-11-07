using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class Household : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string DefaultCulture { get; set; } = "fa-IR";
    public CalendarType DefaultCalendar { get; set; } = CalendarType.Jalali;
    public string DefaultCurrency { get; set; } = "IRR"; // ISO 4217: IRR or IRT
    public string TimeZone { get; set; } = "Asia/Tehran";
    public bool IsActive { get; set; } = true;

    // Navigation collections
    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();
    public ICollection<Property> Properties { get; set; } = new List<Property>();
}
