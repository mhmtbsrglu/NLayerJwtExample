namespace CoreLayer.Models.Base.Concretes;

public abstract class BaseEntity : IBaseEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedDate { get; init; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; protected set; }
    public bool IsDeleted { get; protected set; }
}