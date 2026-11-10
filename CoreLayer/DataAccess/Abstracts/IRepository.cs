using CoreLayer.Models.Base;

namespace CoreLayer.DataAccess.Abstracts;

public interface IRepository<T> where T : IBaseEntity
{
    Task<T> GetItemByIdAsync(Guid id);
    Task<IEnumerable<T>> ListItemsAsync();
    Task<bool> CreateItemAsync(T item);
    Task<bool> UpdateItemAsync(T item);
    Task<bool> DeleteItemAsync(Guid id);    
}