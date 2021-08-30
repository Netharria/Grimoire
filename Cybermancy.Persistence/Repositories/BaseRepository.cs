using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Persistence;

namespace Cybermancy.Persistance.Repositories
{
    public class BaseRepository<T> : IAsyncRepository<T> where T : class
    {
        protected readonly CybermancyDbContext _cybermancyDb;

        public BaseRepository(CybermancyDbContext technomancyDb)
        {
            _cybermancyDb = technomancyDb;
        }

        public async Task<IReadOnlyList<T>> GetAllAsync()
        {
            return await _cybermancyDb.Set<T>().ToListAsync();
        }

        public async Task<T> AddAsync(T entity)
        {
            await _cybermancyDb.Set<T>().AddAsync(entity);
            await _cybermancyDb.SaveChangesAsync();

            return entity;
        }

        public async Task UpdateAsync(T entity)
        {
            _cybermancyDb.Entry(entity).State = EntityState.Modified;
            await _cybermancyDb.SaveChangesAsync();
        }

        public async Task DeleteAsync(T entity)
        {
            _cybermancyDb.Set<T>().Remove(entity);
            await _cybermancyDb.SaveChangesAsync();
        }
    }
}
