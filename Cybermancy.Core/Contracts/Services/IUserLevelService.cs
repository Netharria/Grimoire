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
    /// Manages calls for getting and saving <see cref="UserLevel" />.
    /// </summary>
    public interface IUserLevelService
    {
        // Task<bool> IsUserIgnored(DiscordMember member, out UserLevels userLevels);

        /// <summary>
        /// Gets all ignored <see cref="UserLevel" /> for a specific guild.
        /// </summary>
        /// <param name="guildId">The Guild Id for who to get the ignored <see cref="UserLevel" />s.</param>
        /// <returns>All Users who are ignored for xp gain in the guild.</returns>
        Task<ICollection<UserLevel>> GetAllIgnoredUsersAsync(ulong guildId);

        /// <summary>
        /// Gets a <see cref="UserLevel" />.
        /// </summary>
        /// <param name="userId">The user id for who to get their <see cref="UserLevel" />.</param>
        /// <param name="guildId">The guild for which the user belongs.</param>
        /// <returns>The user who matches the Id.</returns>
        Task<UserLevel> GetUserLevelAsync(ulong userId, ulong guildId);

        /// <summary>
        /// Gets a <see cref="UserLevel" />. Creates a new <see cref="User"/> if one does not exist.
        /// </summary>
        /// <param name="member">The member who to get the user level for.</param>
        /// <returns>The user who matches the Id.</returns>
        Task<UserLevel> GetOrCreateUserLevelAsync(DiscordUser member, ulong guildId);

        /// <summary>
        /// Saves a <see cref="UserLevel" />.
        /// </summary>
        /// <param name="userLevel">The <see cref="UserLevel" /> to save.</param>
        /// <returns>The <see cref="UserLevel" /> after being saved.</returns>
        Task<UserLevel> SaveAsync(UserLevel userLevel);

        /// <summary>
        /// Gets all <see cref="UserLevel" /> for the guild ordered from most XP to least.
        /// </summary>
        /// <param name="guildId">The guild to get the ranked <see cref="UserLevel" /> for. </param>
        /// <returns>All <see cref="UserLevel" /> for the guild ordered from most XP to least.</returns>
        Task<IList<UserLevel>> GetRankedUsersAsync(ulong guildId);
    }
}
