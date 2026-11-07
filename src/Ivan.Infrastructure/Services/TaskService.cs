using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class TaskService : ITaskService
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;

    public TaskService(IIvanDbContext db, ICurrentUserContext userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    public async Task<IReadOnlyList<TaskItem>> GetTasksAsync(TaskItemStatus? status = null, TaskPriority? priority = null, Guid? propertyId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.TaskItems.Include(t => t.Property).AsNoTracking();

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (priority.HasValue)
            query = query.Where(t => t.Priority == priority.Value);

        if (propertyId.HasValue)
            query = query.Where(t => t.PropertyId == propertyId.Value);

        var list = await query.ToListAsync(cancellationToken);
        return list.OrderBy(t => t.DueAt).ToList();
    }

    public async Task<TaskItem?> GetTaskByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.TaskItems
            .Include(t => t.Property)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<TaskItem> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var task = new TaskItem
        {
            Title = request.Title,
            Details = request.Details,
            DueAt = request.DueAt,
            PropertyId = request.PropertyId,
            AssigneePersonId = request.AssigneePersonId,
            Priority = request.Priority,
            RecurrenceId = request.RecurrenceId,
            LinkType = request.LinkType,
            LinkId = request.LinkId,
            Status = TaskItemStatus.Todo
        };

        _db.TaskItems.Add(task);
        await _db.SaveChangesAsync(cancellationToken);
        return task;
    }

    public async Task<bool> UpdateStatusAsync(Guid taskId, TaskItemStatus newStatus, CancellationToken cancellationToken = default)
    {
        var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task == null) return false;

        task.Status = newStatus;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task == null) return false;

        _db.TaskItems.Remove(task);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
