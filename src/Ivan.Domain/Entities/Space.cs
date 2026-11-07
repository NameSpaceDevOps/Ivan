using Ivan.Domain.Common;
using Ivan.Domain.Enums;

namespace Ivan.Domain.Entities;

public class Space : BaseTenantEntity
{
    public Guid PropertyId { get; set; }
    public Guid? ParentSpaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SpaceType Type { get; set; } = SpaceType.Room;

    // Navigation
    public Property? Property { get; set; }
    public Space? ParentSpace { get; set; }
    public ICollection<Space> SubSpaces { get; set; } = new List<Space>();
}
