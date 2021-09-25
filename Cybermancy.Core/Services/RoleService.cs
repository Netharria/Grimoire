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
    /// <summary>
    /// Service class that manages different role interactions.
    /// </summary>
    public class RoleService : IRoleService
    {
        private readonly IAsyncIdRepository<Role> _roleRepository;
        private readonly IAsyncIdRepository<Guild> _guildRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="RoleService"/> class.
        /// </summary>
        /// <param name="roleRepository">The role repository. This should be dependancy injected.</param>
        public RoleService(IAsyncIdRepository<Role> roleRepository, IAsyncIdRepository<Guild> guildRepository)
        {
            this._roleRepository = roleRepository;
            this._guildRepository = guildRepository;
        }

        /// <inheritdoc/>
        public async Task<bool> AreAnyRolesIgnoredAsync(ICollection<DiscordRole> roles, DiscordGuild guild)
        {
            List<Role> databaseRoles = new ();
            foreach (var role in roles)
            {
                databaseRoles.Add(await this.GetOrCreateRoleAsync(role, guild));
            }

            return databaseRoles.Any(x => x.IsXpIgnored);
        }

        /// <inheritdoc/>
        public async Task<Role> SaveAsync(Role role)
        {
            if (await this._roleRepository.ExistsAsync(role.Id))
                return await this._roleRepository.UpdateAsync(role);
            return await this._roleRepository.AddAsync(role);
        }

        /// <inheritdoc/>
        public async ValueTask<Role> GetOrCreateRoleAsync(DiscordRole role, DiscordGuild guild)
        {
            if (await this._roleRepository.ExistsAsync(role.Id)) return await this._roleRepository.GetByIdAsync(role.Id);
            var newRole = new Role()
            {
                GuildId = guild.Id,
                Id = role.Id,
            };
            return await this.SaveAsync(newRole);
        }

        /// <inheritdoc/>
        public ValueTask<Role> GetRoleAsync(ulong roleId) => this._roleRepository.GetByIdAsync(roleId);

        /// <inheritdoc/>
        public Task SetupAllRolesAsync(IEnumerable<DiscordGuild> guilds)
        {
            var newRoles = new List<Role>();
            foreach (var guild in guilds)
            {
                foreach (var role in guild.Roles.Values.Where(x => !this._roleRepository.ExistsAsync(x.Id).Result))
                {
                    newRoles.Add(new Role()
                    {
                        GuildId = guild.Id,
                        Id = role.Id,
                    });
                }
            }

            return this._roleRepository.AddMultipleAsync(newRoles);
        }

        /// <inheritdoc/>
        public async Task<ICollection<Role>> GetAllIgnoredRolesAsync(ulong guildId)
        {
            var guild = await this._guildRepository.GetByIdAsync(guildId);
            if (guild is null) throw new ArgumentNullException(nameof(guildId));
            var roles = guild.Roles.Where(x => x.IsXpIgnored).ToList();
            return roles;
        }
    }
}
