using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Contracts.Services
{
    public interface IRewardService
    {
        //Task<Reward> GetAllGuildRewards(ulong guildId);
        Task<ICollection<DiscordRole>> GrantRewardsMissingFromUser(DiscordMember member);
        
        Task<Reward> Save(Reward reward);
        Task<ICollection<Reward>> GetAllGuildRewards(ulong guildId);
    }
}