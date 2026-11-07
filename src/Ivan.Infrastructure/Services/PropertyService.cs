using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class PropertyService : IPropertyService
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;

    public PropertyService(IIvanDbContext db, ICurrentUserContext userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    public async Task<IReadOnlyList<Property>> GetPropertiesAsync(CancellationToken cancellationToken = default)
    {
        var query = _db.Properties.Include(p => p.Spaces).AsNoTracking();

        // Enforce property scope (Story 1.5 Acceptance Criteria)
        if (_userContext.AllowedPropertyIds != null)
        {
            query = query.Where(p => _userContext.AllowedPropertyIds.Contains(p.Id));
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<Property?> GetPropertyByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_userContext.HasPropertyAccess(id))
            return null;

        return await _db.Properties
            .Include(p => p.Spaces)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Property> CreatePropertyAsync(CreatePropertyRequest request, CancellationToken cancellationToken = default)
    {
        var property = new Property
        {
            Name = request.Name,
            Type = request.Type,
            Address = request.Address,
            Notes = request.Notes
        };

        _db.Properties.Add(property);
        await _db.SaveChangesAsync(cancellationToken);
        return property;
    }

    public async Task<bool> DeletePropertyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_userContext.HasPropertyAccess(id))
            return false;

        var prop = await _db.Properties.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (prop == null) return false;

        _db.Properties.Remove(prop); // Intercepted as soft delete
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<Space>> GetSpacesByPropertyIdAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        if (!_userContext.HasPropertyAccess(propertyId))
            return Array.Empty<Space>();

        return await _db.Spaces
            .Where(s => s.PropertyId == propertyId)
            .Include(s => s.SubSpaces)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Space> CreateSpaceAsync(CreateSpaceRequest request, CancellationToken cancellationToken = default)
    {
        if (!_userContext.HasPropertyAccess(request.PropertyId))
            throw new UnauthorizedAccessException("دسترسی به این ملک مجاز نمی‌باشد.");

        var space = new Space
        {
            PropertyId = request.PropertyId,
            ParentSpaceId = request.ParentSpaceId,
            Name = request.Name,
            Type = request.Type
        };

        _db.Spaces.Add(space);
        await _db.SaveChangesAsync(cancellationToken);
        return space;
    }

    public async Task<bool> DeleteSpaceAsync(Guid spaceId, CancellationToken cancellationToken = default)
    {
        var space = await _db.Spaces.FirstOrDefaultAsync(s => s.Id == spaceId, cancellationToken);
        if (space == null) return false;

        if (!_userContext.HasPropertyAccess(space.PropertyId))
            return false;

        _db.Spaces.Remove(space);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
