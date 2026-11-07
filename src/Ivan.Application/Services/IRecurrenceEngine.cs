using Ivan.Domain.Entities;

namespace Ivan.Application.Services;

public interface IRecurrenceEngine
{
    DateOnly? GetNextOccurrence(Recurrence recurrence, DateOnly afterDate);
    IReadOnlyList<DateOnly> GetOccurrences(Recurrence recurrence, DateOnly fromDate, DateOnly toDate, int maxCount = 50);
}
