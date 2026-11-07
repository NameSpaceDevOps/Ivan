using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class ChecklistService : IChecklistService
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;

    public ChecklistService(IIvanDbContext db, ICurrentUserContext userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    public async Task<IReadOnlyList<Checklist>> GetChecklistsAsync(bool templatesOnly = false, CancellationToken cancellationToken = default)
    {
        return await _db.Checklists
            .Where(c => c.IsTemplate == templatesOnly)
            .Include(c => c.Items)
            .OrderByDescending(c => c.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Checklist?> GetChecklistByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Checklists
            .Include(c => c.Items.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Checklist> CreateChecklistAsync(CreateChecklistRequest request, CancellationToken cancellationToken = default)
    {
        var checklist = new Checklist
        {
            Title = request.Title,
            OwnerType = request.OwnerType,
            OwnerId = request.OwnerId,
            IsTemplate = request.IsTemplate,
            Visibility = request.Visibility
        };

        _db.Checklists.Add(checklist);
        await _db.SaveChangesAsync(cancellationToken);
        return checklist;
    }

    public async Task<Checklist> CloneFromTemplateAsync(Guid templateId, string newTitle, CancellationToken cancellationToken = default)
    {
        var template = await _db.Checklists
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == templateId, cancellationToken);

        if (template == null)
            throw new ArgumentException("قالب چک‌لیست یافت نشد.", nameof(templateId));

        var clone = new Checklist
        {
            Title = newTitle,
            TemplateId = template.Id,
            IsTemplate = false,
            Visibility = template.Visibility,
            OwnerType = template.OwnerType,
            OwnerId = template.OwnerId
        };

        _db.Checklists.Add(clone);

        // Copy top-level and nested items
        var itemMap = new Dictionary<Guid, Guid>(); // oldId -> newId

        foreach (var oldItem in template.Items.OrderBy(i => i.SortOrder))
        {
            var newItem = new ChecklistItem
            {
                ChecklistId = clone.Id,
                Title = oldItem.Title,
                SortOrder = oldItem.SortOrder,
                AssigneePersonId = oldItem.AssigneePersonId,
                Done = false
            };
            itemMap[oldItem.Id] = newItem.Id;
            clone.Items.Add(newItem);
        }

        // Remap parent IDs
        foreach (var oldItem in template.Items.Where(i => i.ParentItemId.HasValue))
        {
            var newId = itemMap[oldItem.Id];
            var newItem = clone.Items.First(i => i.Id == newId);
            if (oldItem.ParentItemId.HasValue && itemMap.TryGetValue(oldItem.ParentItemId.Value, out var newParentId))
            {
                newItem.ParentItemId = newParentId;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return clone;
    }

    public async Task<ChecklistItem> AddItemAsync(AddChecklistItemRequest request, CancellationToken cancellationToken = default)
    {
        var checklist = await _db.Checklists.FindAsync([request.ChecklistId], cancellationToken);
        if (checklist == null)
            throw new ArgumentException("چک‌لیست یافت نشد.", nameof(request.ChecklistId));

        var count = await _db.ChecklistItems
            .CountAsync(i => i.ChecklistId == request.ChecklistId && i.ParentItemId == request.ParentItemId, cancellationToken);

        var item = new ChecklistItem
        {
            ChecklistId = request.ChecklistId,
            ParentItemId = request.ParentItemId,
            Title = request.Title,
            DueAt = request.DueAt,
            AssigneePersonId = request.AssigneePersonId,
            SortOrder = count
        };

        _db.ChecklistItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<bool> ToggleItemDoneAsync(Guid itemId, bool done, CancellationToken cancellationToken = default)
    {
        var item = await _db.ChecklistItems.FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (item == null) return false;

        item.Done = done;
        item.DoneAt = done ? DateTimeOffset.UtcNow : null;
        item.DoneBy = done ? _userContext.UserId : null;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteChecklistAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var checklist = await _db.Checklists.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (checklist == null) return false;

        _db.Checklists.Remove(checklist); // soft delete
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
