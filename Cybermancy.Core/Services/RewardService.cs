using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Services;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Services
{
    public class RewardService : IRewardService
    {
        public Task<ICollection<DiscordRole>> GrantRewardsMissingFromUser(DiscordMember member)
        {
            throw new System.NotImplementedException();
        }

        public Task<Reward> Save(Reward reward)
        {
            throw new System.NotImplementedException();
        }
    }
}