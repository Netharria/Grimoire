// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

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

        /// <summary>
        /// Initializes a new instance of the <see cref="RewardService"/> class.
        /// </summary>
        /// <param name="rewardRepository"></param>
        /// <param name="guildRepository"></param>
        /// <param name="roleRepository"></param>
        public RewardService(IRewardRepository rewardRepository, IAsyncIdRepository<Guild> guildRepository, IAsyncIdRepository<Role> roleRepository)
        {
            this._rewardRepository = rewardRepository;
            this._guildRepository = guildRepository;
            this._roleRepository = roleRepository;
        }

        public async Task<ICollection<Reward>> GetAllGuildRewardsAsync(ulong guildId)
        {
            var guild = await this._guildRepository.GetByIdAsync(guildId);
            return guild.Rewards;
        }

        public ValueTask<Reward> GetRewardAsync(ulong guildId, ulong roleId) => this._rewardRepository.GetByPrimaryKeyAsync(roleId);

        public async Task<ICollection<DiscordRole>> GrantRewardsMissingFromUserAsync(DiscordMember member)
        {
            var guild = await this._guildRepository.GetByIdAsync(member.Guild.Id);
            if (!guild.LevelSettings.IsLevelingEnabled) return new List<DiscordRole>();
            var rewardsToAdd = guild.Rewards.Where(x =>
                member.Roles.All(a => a.Id != x.RoleId)).ToList();
            var rolesToAdd = rewardsToAdd.Select(async x =>
            {
                try
                {
                    var role = member.Guild.GetRole(x.RoleId);
                    if (role is not null) return role;
                    await this._roleRepository.DeleteAsync(x.Role);
                    Console.WriteLine(
                        $"Role:{x.Role.Id} from Guild:{member.Guild.Name}({member.Guild.Id}) not found. " +
                        $"Removing from database");

                    // Post log to guild main log channel
                    return null;
                }
                catch (Exception e)
                {
                    Console.WriteLine(
                        $"Exception thrown while getting Role:{x.Role.Id} from Guild:{member.Guild.Name}({member.Guild.Id}). {e}");
                    return null;
                }
            }).Select(x => x.Result).ToList();
            if (!rolesToAdd.Any()) return rolesToAdd;
            var roles = member.Roles.Concat(rolesToAdd).ToList();
            await member.ReplaceRolesAsync(roles);

            return rolesToAdd;
        }

        public Task<Reward> SaveAsync(Reward reward)
        {
            if (this._rewardRepository.Exists(reward.RoleId))
                return this._rewardRepository.UpdateAsync(reward);
            return this._rewardRepository.AddAsync(reward);
        }
    }
}
