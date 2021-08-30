using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Persistence;

namespace Cybermancy.Persistance.Repositories
{
    public class BaseRepository<T> : IAsyncRepository<T> where T : class
    {
        protected readonly CybermancyDbContext _technomancyDb;

        public BaseRepository(CybermancyDbContext technomancyDb)
        {
            _technomancyDb = technomancyDb;
        }

        public async Task<IReadOnlyList<T>> GetAllAsync()
        {
            return await _technomancyDb.Set<T>().ToListAsync();
        }

        public async Task<T> AddAsync(T entity)
        {
            await _technomancyDb.Set<T>().AddAsync(entity);
            await _technomancyDb.SaveChangesAsync();

            return entity;
        }

        public async Task UpdateAsync(T entity)
        {
            _technomancyDb.Entry(entity).State = EntityState.Modified;
            await _technomancyDb.SaveChangesAsync();
        }

        public async Task DeleteAsync(T entity)
        {
            _technomancyDb.Set<T>().Remove(entity);
            await _technomancyDb.SaveChangesAsync();
        }
    }
}
