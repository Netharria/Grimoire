// -----------------------------------------------------------------------
// <copyright file="IRoleService.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Core.Contracts.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cybermancy.Domain;
    using DSharpPlus.Entities;

    /// <summary>
    /// Service class that manages different role interactions.
    /// </summary>
    public interface IRoleService
    {
        /// <summary>
        /// Checks if any roles are ignored for xp gain.
        /// </summary>
        /// <param name="roles">Roles to check ignored status.</param>
        /// <param name="guild">The Guild the roles belong to.</param>
        /// <returns>The boolean for if any role is ignored.</returns>
        Task<bool> AreAnyRolesIgnoredAsync(ICollection<DiscordRole> roles, DiscordGuild guild);

        /// <summary>
        /// Gets all the roles that are ignored for xp gain in a guild.
        /// </summary>
        /// <param name="guildId">The guild to get all the ignored roles for.</param>
        /// <returns>A list of all roles that are ignored for xp gain.</returns>
        Task<ICollection<Role>> GetAllIgnoredRolesAsync(ulong guildId);

        /// <summary>
        /// Saves a role into the database.
        /// </summary>
        /// <param name="role">The role to save.</param>
        /// <returns>The saved role.</returns>
        Task<Role> SaveAsync(Role role);

        /// <summary>
        /// Gets a role out of the database. If no role is found creates a new role and saves it to the database.
        /// </summary>
        /// <param name="role">The role to find in the database.</param>
        /// <param name="guild">The guild the role belongs to.</param>
        /// <returns>The role from the database.</returns>
        ValueTask<Role> GetRoleAsync(DiscordRole role, DiscordGuild guild);

        /// <summary>
        /// Gets a role out of the database.
        /// </summary>
        /// <param name="roleId">The id of the role to find in the database.</param>
        /// <returns>The role from the database.</returns>
        ValueTask<Role> GetRoleAsync(ulong roleId);

        /// <summary>
        /// Adds all roles from the provided guilds into the database.
        /// </summary>
        /// <param name="guilds">A collection of guilds whos roles should be saved to the database.</param>
        /// <returns>The completed task.</returns>
        Task SetupAllRolesAsync(IEnumerable<DiscordGuild> guilds);
    }
}