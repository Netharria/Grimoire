using System.Linq;
using Cybermancy.Core.Contracts.Persistence;
using Cybermancy.Domain;

namespace Cybermancy.Persistence.Repositories
{
    public class RewardRepository : BaseRepository<Reward>, IRewardRepository
    {
        public RewardRepository(CybermancyDbContext cybermancyDb) : base(cybermancyDb)
        {
        }

        public bool Exists(ulong roleId)
        {
            return CybermancyDb.Rewards.Any(x => x.RoleId == roleId);
        }
    }
}