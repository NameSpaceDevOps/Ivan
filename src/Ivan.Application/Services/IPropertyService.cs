using Ivan.Domain.Entities;
using Ivan.Domain.Enums;

namespace Ivan.Application.Services;

public record CreatePropertyRequest(string Name, PropertyType Type, string? Address = null, string? Notes = null);
public record CreateSpaceRequest(Guid PropertyId, string Name, SpaceType Type, Guid? ParentSpaceId = null);

public interface IPropertyService
{
    Task<IReadOnlyList<Property>> GetPropertiesAsync(CancellationToken cancellationToken = default);
    Task<Property?> GetPropertyByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Property> CreatePropertyAsync(CreatePropertyRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeletePropertyAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Space>> GetSpacesByPropertyIdAsync(Guid propertyId, CancellationToken cancellationToken = default);
    Task<Space> CreateSpaceAsync(CreateSpaceRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteSpaceAsync(Guid spaceId, CancellationToken cancellationToken = default);
}
