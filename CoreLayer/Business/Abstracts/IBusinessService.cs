using CoreLayer.Models.Base;

namespace CoreLayer.Business.Abstracts;

public interface IBusinessService<T> where T : IBaseEntity
{
    public Task<T?> GetItemByIdAsync(Guid id);
    public Task<IEnumerable<T>> ListItemsAsync();
    public Task<bool> CreateItemAsync(T item);
    public Task<bool> UpdateItemAsync(T item);
    public Task<bool> DeleteItemAsync(Guid id);
}