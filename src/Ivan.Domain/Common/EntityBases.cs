namespace Ivan.Domain.Common;

public interface IEntity
{
    Guid Id { get; }
}

public interface ITenantEntity : IEntity
{
    Guid HouseholdId { get; set; }
}

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}

public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; set; }
    bool IsDeleted => DeletedAt.HasValue;
}

public abstract class BaseEntity : IEntity, IAuditable, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public byte[]? RowVersion { get; set; }
}

public abstract class BaseTenantEntity : BaseEntity, ITenantEntity
{
    public Guid HouseholdId { get; set; }
}
