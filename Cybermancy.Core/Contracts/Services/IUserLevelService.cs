using System.Threading.Tasks;
using Cybermancy.Domain;

namespace Cybermancy.Core.Contracts.Services
{
    public interface IUserLevelService
    {
        
        //Task<bool> IsUserIgnored(DiscordMember member, out UserLevels userLevels);
        Task<UserLevel> GetUserLevels(ulong userId, ulong guildId);
        Task<UserLevel> Save(UserLevel userLevel);
    }
}