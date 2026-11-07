using Ivan.Domain.Entities;
using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public record CreateChecklistRequest(string Title, string? OwnerType = null, Guid? OwnerId = null, bool IsTemplate = false, ResourceVisibility Visibility = ResourceVisibility.Household);
public record AddChecklistItemRequest(Guid ChecklistId, string Title, Guid? ParentItemId = null, DateTimeOffset? DueAt = null, Guid? AssigneePersonId = null);

public interface IChecklistService
{
    Task<IReadOnlyList<Checklist>> GetChecklistsAsync(bool templatesOnly = false, CancellationToken cancellationToken = default);
    Task<Checklist?> GetChecklistByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Checklist> CreateChecklistAsync(CreateChecklistRequest request, CancellationToken cancellationToken = default);
    Task<Checklist> CloneFromTemplateAsync(Guid templateId, string newTitle, CancellationToken cancellationToken = default);
    Task<ChecklistItem> AddItemAsync(AddChecklistItemRequest request, CancellationToken cancellationToken = default);
    Task<bool> ToggleItemDoneAsync(Guid itemId, bool done, CancellationToken cancellationToken = default);
    Task<bool> DeleteChecklistAsync(Guid id, CancellationToken cancellationToken = default);
}
