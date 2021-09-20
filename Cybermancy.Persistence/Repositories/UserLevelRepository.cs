using System.Collections.Generic;
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

        public Task<IList<UserLevel>> GetRankedGuildUsers(ulong guildId)
        {
            IList<UserLevel> result = CybermancyDb.UserLevels.Where(x => x.GuildId == guildId).OrderByDescending(x => x.Xp).ToList();
            return Task.FromResult(result);
        }
    }
}