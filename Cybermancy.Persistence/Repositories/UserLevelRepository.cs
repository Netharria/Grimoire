using System.Linq;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Persistence;
using Cybermancy.Domain;

namespace Cybermancy.Persistence.Repositories
{
    public class UserLevelRepository : BaseRepository<UserLevel>, IUserLevelRepository
    {
        public UserLevelRepository(CybermancyDbContext cybermancyDb) : base(cybermancyDb)
        {
        }

        public Task<UserLevel> GetUserLevel(ulong userId, ulong guildId)
        {
            var result = CybermancyDb.UserLevels.FirstOrDefault(x => x.UserId == userId && x.GuildId == guildId);
            return Task.FromResult(result);
        }

        public bool Exists(ulong userId, ulong guildId)
        {
            return CybermancyDb.UserLevels.Any(x => x.UserId == userId && x.GuildId == guildId);
        }
    }
}