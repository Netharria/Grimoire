using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Domain;

namespace Cybermancy.Core.Contracts.Services
{
    public interface IUserLevelService
    {

        //Task<bool> IsUserIgnored(DiscordMember member, out UserLevels userLevels);
        Task<ICollection<UserLevel>> GetAllIgnoredUsers(ulong guildId);
        Task<UserLevel> GetUserLevel(ulong userId, ulong guildId);
        Task<UserLevel> Save(UserLevel userLevel);
        Task<IList<UserLevel>> GetRankedUsers(ulong guildId);
    }
}