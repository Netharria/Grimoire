using System.Linq;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Persistence;
using Cybermancy.Domain.Shared;

namespace Cybermancy.Persistance.Repositories
{
    public class BaseIdRepository<T> : BaseRepository<T>, IAsyncIdRepository<T> where T : Identifiable
    {
        public BaseIdRepository(CybermancyDbContext technomancyDb) : base (technomancyDb) { }

        public bool Exists(ulong id)
        {
            return _cybermancyDb.Set<T>().Any(x => x.Id == id);
        }

        public virtual async Task<T> GetByIdAsync(ulong id)
        {
            return await _cybermancyDb.Set<T>().FindAsync(id);
        }
    }
}
