// -----------------------------------------------------------------------
// <copyright file="IRewardService.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Contracts.Services
{
    public interface IRewardService
    {
        // Task<Reward> GetAllGuildRewards(ulong guildId);
        /// <summary>
        ///
        /// </summary>
        /// <param name="member"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<ICollection<DiscordRole>> GrantRewardsMissingFromUserAsync(DiscordMember member);

        /// <summary>
        ///
        /// </summary>
        /// <param name="reward"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<Reward> SaveAsync(Reward reward);

        /// <summary>
        ///
        /// </summary>
        /// <param name="guildId"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<ICollection<Reward>> GetAllGuildRewardsAsync(ulong guildId);
    }
}