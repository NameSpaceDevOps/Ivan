using Ivan.Domain.Entities;
using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public record StoreAttachmentRequest(
    string FileName,
    string ContentType,
    string OwnerType,
    Guid OwnerId,
    Stream ContentStream,
    ResourceVisibility Visibility = ResourceVisibility.Household);

public interface IAttachmentStorage
{
    Task<Attachment> StoreAsync(StoreAttachmentRequest request, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(Guid attachmentId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Attachment>> GetAttachmentsByOwnerAsync(string ownerType, Guid ownerId, CancellationToken cancellationToken = default);
}
