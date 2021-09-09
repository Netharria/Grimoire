using System.Linq;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Persistence;
using Cybermancy.Domain.Shared;

namespace Cybermancy.Persistence.Repositories
{
    public class BaseIdRepository<T> : BaseRepository<T>, IAsyncIdRepository<T> where T : Identifiable
    {
        public BaseIdRepository(CybermancyDbContext cybermancyDb) : base(cybermancyDb)
        {
        }

        public bool Exists(ulong id)
        {
            return CybermancyDb.Set<T>().Any(x => x.Id == id);
        }

        public virtual async Task<T> GetByIdAsync(ulong id)
        {
            return await CybermancyDb.Set<T>().FindAsync(id);
        }
    }
}