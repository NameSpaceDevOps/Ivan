using Ivan.Domain.Entities;
using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public record CreateTaskRequest(
    string Title,
    string? Details = null,
    DateTimeOffset? DueAt = null,
    Guid? PropertyId = null,
    Guid? AssigneePersonId = null,
    TaskPriority Priority = TaskPriority.Medium,
    Guid? RecurrenceId = null,
    string? LinkType = null,
    Guid? LinkId = null);

public interface ITaskService
{
    Task<IReadOnlyList<TaskItem>> GetTasksAsync(TaskItemStatus? status = null, TaskPriority? priority = null, Guid? propertyId = null, CancellationToken cancellationToken = default);
    Task<TaskItem?> GetTaskByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskItem> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateStatusAsync(Guid taskId, TaskItemStatus newStatus, CancellationToken cancellationToken = default);
    Task<bool> DeleteTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
}
