using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Persistence.Repositories
{
    public class BaseRepository<T> : IAsyncRepository<T> where T : class
    {
        protected readonly CybermancyDbContext CybermancyDb;

        public BaseRepository(CybermancyDbContext cybermancyDb)
        {
            CybermancyDb = cybermancyDb;
        }

        public async Task<IReadOnlyList<T>> GetAllAsync()
        {
            return await CybermancyDb.Set<T>().ToListAsync();
        }

        public async Task<T> AddAsync(T entity)
        {
            await CybermancyDb.Set<T>().AddAsync(entity);
            await CybermancyDb.SaveChangesAsync();

            return entity;
        }
        public async Task<ICollection<T>> AddMultipleAsync(ICollection<T> entities)
        {
            await CybermancyDb.Set<T>().AddRangeAsync(entities);
            await CybermancyDb.SaveChangesAsync();

            return entities;
        }

        public async Task<T> UpdateAsync(T entity)
        {
            CybermancyDb.Entry(entity).State = EntityState.Modified;
            await CybermancyDb.SaveChangesAsync();
            return entity;
        }

        public async Task DeleteAsync(T entity)
        {
            CybermancyDb.Set<T>().Remove(entity);
            await CybermancyDb.SaveChangesAsync();
        }

        public async Task<T> GetByPrimaryKey(params object[] keys)
        {
            return await CybermancyDb.Set<T>().FindAsync(keys);
        }
    }
}