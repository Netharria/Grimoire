using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Domain;

namespace Cybermancy.Core.Contracts.Persistence
{
    public interface IUserLevelRepository : IAsyncRepository<UserLevel>
    {
        Task<UserLevel> GetUserLevel(ulong userId, ulong guildId);
        bool Exists(ulong userId, ulong guildId);
        Task<IList<UserLevel>> GetRankedGuildUsers(ulong guildId);
    }
}