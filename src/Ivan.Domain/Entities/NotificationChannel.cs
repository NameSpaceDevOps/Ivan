using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class NotificationChannel : BaseTenantEntity
{
    public Guid UserId { get; set; }
    public NotificationChannelType Type { get; set; } = NotificationChannelType.WebPush;
    public string Address { get; set; } = string.Empty; // ChatId, phone number, or VAPID subscription JSON
    public bool Verified { get; set; } = false;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 1;
}

public class NotificationLog : BaseTenantEntity
{
    public Guid ReminderId { get; set; }
    public Guid ChannelId { get; set; }
    public NotificationDeliveryStatus Status { get; set; } = NotificationDeliveryStatus.Queued;
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public string? Error { get; set; }

    // Navigation
    public Reminder? Reminder { get; set; }
    public NotificationChannel? Channel { get; set; }
}
