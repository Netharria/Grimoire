// -----------------------------------------------------------------------
// <copyright file="RoleService.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Core.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cybermancy.Core.Contracts.Persistence;
    using Cybermancy.Core.Contracts.Services;
    using Cybermancy.Domain;
    using DSharpPlus.Entities;

    /// <summary>
    /// Service class that manages different role interactions.
    /// </summary>
    public class RoleService : IRoleService
    {
        private readonly IAsyncIdRepository<Role> roleRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="RoleService"/> class.
        /// </summary>
        /// <param name="roleRepository">The role repository. This should be dependancy injected.</param>
        public RoleService(IAsyncIdRepository<Role> roleRepository)
        {
            this.roleRepository = roleRepository;
        }

        /// <inheritdoc/>
        public async Task<bool> AreAnyRolesIgnoredAsync(ICollection<DiscordRole> roles, DiscordGuild guild)
        {
            List<Role> databaseRoles = new ();
            foreach (var role in roles)
            {
                databaseRoles.Add(await this.GetRoleAsync(role, guild));
            }

            return databaseRoles.Any(x => x.IsXpIgnored);
        }

        /// <inheritdoc/>
        public async Task<Role> SaveAsync(Role role)
        {
            if (await this.roleRepository.ExistsAsync(role.Id))
                return await this.roleRepository.UpdateAsync(role);
            return await this.roleRepository.AddAsync(role);
        }

        /// <inheritdoc/>
        public async ValueTask<Role> GetRoleAsync(DiscordRole role, DiscordGuild guild)
        {
            if (await this.roleRepository.ExistsAsync(role.Id)) return await this.roleRepository.GetByIdAsync(role.Id);
            var newRole = new Role()
            {
                GuildId = guild.Id,
                Id = role.Id,
            };
            return await this.SaveAsync(newRole);
        }

        /// <inheritdoc/>
        public ValueTask<Role> GetRoleAsync(ulong roleId)
        {
            return this.roleRepository.GetByIdAsync(roleId);
        }

        /// <inheritdoc/>
        public Task SetupAllRolesAsync(IEnumerable<DiscordGuild> guilds)
        {
            var newRoles = new List<Role>();
            foreach (var guild in guilds)
            {
                foreach (var role in guild.Roles.Values.Where(x => !this.roleRepository.ExistsAsync(x.Id).Result))
                {
                    newRoles.Add(new Role()
                    {
                        GuildId = guild.Id,
                        Id = role.Id,
                    });
                }
            }

            return this.roleRepository.AddMultipleAsync(newRoles);
        }

        /// <inheritdoc/>
        public Task<ICollection<Role>> GetAllIgnoredRolesAsync(ulong guildId)
        {
            throw new System.NotImplementedException();
        }
    }
}