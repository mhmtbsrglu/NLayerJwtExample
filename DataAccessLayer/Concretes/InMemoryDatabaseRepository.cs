using CoreLayer.DataAccess.Abstracts;
using CoreLayer.Models.Base;

namespace DataAccessLayer.Concretes;

public class InMemoryDatabaseRepository<T> : IRepository<T> where T : IBaseEntity
{
    private readonly List<T> _items = new();
    private readonly object _lock = new();

    public Task<T> GetItemByIdAsync(Guid id)
    {
        lock (_lock)
        {
            return Task.FromResult(_items.FirstOrDefault(item => item.Id == id));
        }
    }

    public Task<IEnumerable<T>> ListItemsAsync()
    {
        lock (_lock)
        {
            return Task.FromResult<IEnumerable<T>>(_items.ToList());
        }
    }

    public Task<bool> CreateItemAsync(T item)
    {
        lock (_lock)
        {
            _items.Add(item);
            return Task.FromResult(true);
        }
    }

    public Task<bool> UpdateItemAsync(T item)
    {
        lock (_lock)
        {
            var index = _items.FindIndex(i => i.Id == item.Id);
            if (index < 0)
                return Task.FromResult(false);

            _items[index] = item;
            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteItemAsync(Guid id)
    {
        lock (_lock)
        {
            var index = _items.FindIndex(i => i.Id == id);
            if (index < 0)
                return Task.FromResult(false);

            _items.RemoveAt(index);
            return Task.FromResult(true);
        }
    }
}