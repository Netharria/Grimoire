// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Contracts.Services
{
    /// <summary>
    /// Service class that manages different reward interactions between discord and the database.
    /// </summary>
    public interface IRewardService
    {
        /// <summary>
        /// Gets all the guilds <see cref="Reward"/>.
        /// </summary>
        /// <param name="guildId">The id for the <see cref="DiscordGuild"/> to get the <see cref="Reward"/> for.</param>
        /// <param name="roleId">The id of the <see cref="DiscordRole"/> to get the <see cref="Reward"/> for.</param>
        /// <returns>All the guilds <see cref="Reward"/></returns>
        ValueTask<Reward> GetRewardAsync(ulong guildId, ulong roleId);

        /// <summary>
        /// Takes the discord member, and applies any <see cref="Reward"/> for the guild that the user should have.
        /// </summary>
        /// <param name="member">The member to apply rewards to.</param>
        /// <returns>The collection of <see cref="DiscordRole"/> that were applied to the user.</returns>
        Task<ICollection<DiscordRole>> GrantRewardsMissingFromUserAsync(DiscordMember member);

        /// <summary>
        /// Saves a new entry or updates a current entry for the provided <see cref="Reward"/>.
        /// </summary>
        /// <param name="reward">The <see cref="Reward"/> to save.</param>
        /// <returns>The saved <see cref="Reward"/>.</returns>
        Task<Reward> SaveAsync(Reward reward);

        /// <summary>
        /// Gets all the guilds <see cref="Reward"/>.
        /// </summary>
        /// <param name="guildId">The id for the guild to get the <see cref="Reward"/> for.</param>
        /// <returns>All the guilds <see cref="Reward"/></returns>
        Task<ICollection<Reward>> GetAllGuildRewardsAsync(ulong guildId);
    }
}
