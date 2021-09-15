using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Persistence;
using Cybermancy.Core.Contracts.Services;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Services
{
    public class RewardService : IRewardService
    {
        private readonly IRewardRepository _rewardRepository;
        private readonly IAsyncIdRepository<Guild> _guildRepository;
        private readonly IAsyncIdRepository<Role> _roleRepository;
        

        public RewardService(IRewardRepository rewardRepository, IAsyncIdRepository<Guild> guildRepository, IAsyncIdRepository<Role> roleRepository)
        {
            _rewardRepository = rewardRepository;
            _guildRepository = guildRepository;
            _roleRepository = roleRepository;
        }

        public async Task<ICollection<Reward>> GetAllGuildRewards(ulong guildId)
        {
            var guild = await _guildRepository.GetByIdAsync(guildId);
            return guild.Rewards;
        }

        public async Task<ICollection<DiscordRole>> GrantRewardsMissingFromUser(DiscordMember member)
        {
            var guild = await _guildRepository.GetByIdAsync(member.Guild.Id);
            if (!guild.LevelSettings.IsLevelingEnabled) return new List<DiscordRole>();
            var rewardsToAdd = guild.Rewards.Where(x =>
                member.Roles.All(a => a.Id != x.RoleId)).ToList();
            var rolesToAdd = rewardsToAdd.Select(x =>
            {
                try
                {
                    var role = member.Guild.GetRole(x.RoleId);
                    if (role is not null) return role;
                    _roleRepository.DeleteAsync(x.Role);
                    Console.WriteLine(
                        $"Role:{x.Role.Id} from Guild:{member.Guild.Name}({member.Guild.Id}) not found. " +
                        $"Removing from database");
                    
                    //Post log to guild main log channel
                    return null;
                }
                catch (Exception e)
                {
                    Console.WriteLine(
                        $"Exception thrown while getting Role:{x.Role.Id} from Guild:{member.Guild.Name}({member.Guild.Id}). {e}");
                    return null;
                }
            }).ToList();
            if (!rolesToAdd.Any()) return rolesToAdd;
            var roles = member.Roles.Concat(rolesToAdd).ToList();
            await member.ReplaceRolesAsync(roles);

            return rolesToAdd;
        }

        public async Task<Reward> Save(Reward reward)
        {
            if (_rewardRepository.Exists(reward.RoleId))
                return await _rewardRepository.UpdateAsync(reward);
            return await _rewardRepository.AddAsync(reward);
        }
    }
}