using System.Linq;
using System.Threading.Tasks;
using Technomancy.Core.Contracts.Persistence;
using Technomancy.Domain.Shared;

namespace Technomancy.Persistance.Repositories
{
    public class BaseIdRepository<T> : BaseRepository<T>, IAsyncIdRepository<T> where T : Identifiable
    {
        public BaseIdRepository(TechnomancyDbContext technomancyDb) : base (technomancyDb) { }

        public bool Exists(ulong id)
        {
            return _technomancyDb.Set<T>().Any(x => x.Id == id);
        }

        public virtual async Task<T> GetByIdAsync(ulong id)
        {
            return await _technomancyDb.Set<T>().FindAsync(id);
        }
    }
}
