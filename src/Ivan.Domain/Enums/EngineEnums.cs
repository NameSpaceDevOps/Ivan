namespace Ivan.Domain.Enums;

public enum TaskItemStatus
{
    Todo = 1,
    InProgress = 2,
    Done = 3,
    Cancelled = 4
}

public enum TaskPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4
}

public enum RecurrenceFrequency
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Yearly = 4
}

public enum ReminderState
{
    Pending = 1,
    Processing = 2,
    Sent = 3,
    Failed = 4,
    Dismissed = 5
}

public enum NotificationChannelType
{
    WebPush = 1,
    Telegram = 2,
    Bale = 3,
    Sms = 4
}

public enum NotificationDeliveryStatus
{
    Queued = 1,
    Sent = 2,
    Failed = 3
}
