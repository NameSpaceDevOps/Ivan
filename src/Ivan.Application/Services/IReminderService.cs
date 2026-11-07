using Ivan.Domain.Entities;
using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public record ScheduleReminderRequest(
    string SubjectType,
    Guid SubjectId,
    string Title,
    DateTimeOffset FireAt,
    string DedupKey,
    string? Body = null,
    IReadOnlyList<Guid>? RecipientUserIds = null);

public interface IReminderService
{
    Task<Reminder> ScheduleReminderAsync(ScheduleReminderRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Reminder>> GetDuePendingRemindersAsync(DateTimeOffset asOf, int maxCount = 20, CancellationToken cancellationToken = default);
    Task MarkReminderProcessedAsync(Guid reminderId, ReminderState newState, string? error = null, CancellationToken cancellationToken = default);
}
