namespace CoreLayer.Models.Base;

public interface IBaseEntity
{
    Guid Id { get; }
    DateTime CreatedDate { get; }
    DateTime? UpdatedDate { get; }
    bool IsDeleted { get;}
}