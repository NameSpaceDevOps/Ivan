using System.Text.Json;
using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class ReminderService : IReminderService
{
    private readonly IIvanDbContext _db;

    public ReminderService(IIvanDbContext db)
    {
        _db = db;
    }

    public async Task<Reminder> ScheduleReminderAsync(ScheduleReminderRequest request, CancellationToken cancellationToken = default)
    {
        // Idempotency check with DedupKey (Section 3.5 Story 2.5)
        var existing = await _db.Reminders
            .FirstOrDefaultAsync(r => r.DedupKey == request.DedupKey, cancellationToken);

        if (existing != null)
        {
            return existing;
        }

        var reminder = new Reminder
        {
            SubjectType = request.SubjectType,
            SubjectId = request.SubjectId,
            Title = request.Title,
            Body = request.Body,
            FireAt = request.FireAt,
            DedupKey = request.DedupKey,
            State = ReminderState.Pending,
            RecipientUserIdsJson = request.RecipientUserIds != null ? JsonSerializer.Serialize(request.RecipientUserIds) : null
        };

        _db.Reminders.Add(reminder);
        await _db.SaveChangesAsync(cancellationToken);
        return reminder;
    }

    public async Task<IReadOnlyList<Reminder>> GetDuePendingRemindersAsync(DateTimeOffset asOf, int maxCount = 20, CancellationToken cancellationToken = default)
    {
        return await _db.Reminders
            .Where(r => r.State == ReminderState.Pending && r.FireAt <= asOf)
            .OrderBy(r => r.FireAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> MarkReminderProcessedAsync(Guid reminderId, ReminderState newState, string? error = null, CancellationToken cancellationToken = default)
    {
        var reminder = await _db.Reminders.FirstOrDefaultAsync(r => r.Id == reminderId, cancellationToken);
        if (reminder == null) return false;

        reminder.State = newState;
        reminder.Attempts++;
        reminder.LastError = error;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    Task IReminderService.MarkReminderProcessedAsync(Guid reminderId, ReminderState newState, string? error, CancellationToken cancellationToken)
    {
        return MarkReminderProcessedAsync(reminderId, newState, error, cancellationToken);
    }
}
