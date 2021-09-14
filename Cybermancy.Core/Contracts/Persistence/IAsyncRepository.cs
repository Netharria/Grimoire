using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cybermancy.Core.Contracts.Persistence
{
    public interface IAsyncRepository<T> where T : class
    {
        Task<IReadOnlyList<T>> GetAllAsync();
        Task<T> GetByPrimaryKey(params object[] keys);
        Task<T> AddAsync(T entity);
        Task<ICollection<T>> AddMultipleAsync(ICollection<T> entities);
        Task<T> UpdateAsync(T entity);
        Task DeleteAsync(T entity);
    }
}