// -----------------------------------------------------------------------
// <copyright file="GuildService.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Persistence;
using Cybermancy.Core.Contracts.Services;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Services
{
    public class GuildService : IGuildService
    {
        private readonly IAsyncIdRepository<Guild> _guildRepository;
        private readonly IAsyncRepository<GuildLevelSettings> _guildLevelRepository;
        private readonly IAsyncRepository<GuildModerationSettings> _guildModerationRepository;
        private readonly IAsyncRepository<GuildLogSettings> _guildLogRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="GuildService"/> class.
        /// </summary>
        /// <param name="guildRepository"></param>
        /// <param name="guildLevelRepository"></param>
        /// <param name="guildModerationRepository"></param>
        /// <param name="guildLogRepository"></param>
        public GuildService(
            IAsyncIdRepository<Guild> guildRepository,
            IAsyncRepository<GuildLevelSettings> guildLevelRepository,
            IAsyncRepository<GuildModerationSettings> guildModerationRepository,
            IAsyncRepository<GuildLogSettings> guildLogRepository)
        {
            this._guildRepository = guildRepository;
            this._guildLevelRepository = guildLevelRepository;
            this._guildModerationRepository = guildModerationRepository;
            this._guildLogRepository = guildLogRepository;
        }

        public async ValueTask<Guild> GetGuildAsync(DiscordGuild guild)
        {
            if (await this._guildRepository.ExistsAsync(guild.Id)) return await this._guildRepository.GetByIdAsync(guild.Id);
            await this._guildRepository.AddAsync(new Guild()
            {
                Id = guild.Id,
            });
            await this._guildLevelRepository.AddAsync(new GuildLevelSettings()
            {
                GuildId = guild.Id,
            });
            await this._guildLogRepository.AddAsync(new GuildLogSettings()
            {
                GuildId = guild.Id,
            });
            await this._guildModerationRepository.AddAsync(new GuildModerationSettings()
            {
                GuildId = guild.Id,
            });

            return await this._guildRepository.GetByIdAsync(guild.Id);
        }

        public ValueTask<Guild> GetGuildAsync(ulong guildId) => this._guildRepository.GetByIdAsync(guildId);

        public async Task SetupAllGuildAsync(IEnumerable<DiscordGuild> guilds)
        {
            var guildsToAdd = new List<Guild>();
            var guildLevelSettingsToAdd = new List<GuildLevelSettings>();
            var guildLogSettingsToAdd = new List<GuildLogSettings>();
            var guildModerationSettingsToAdd = new List<GuildModerationSettings>();
            foreach (var guild in guilds)
            {
                if (!await this._guildRepository.ExistsAsync(guild.Id))
                {
                    guildsToAdd.Add(new Guild()
                    {
                        Id = guild.Id,
                    });
                    guildLevelSettingsToAdd.Add(new GuildLevelSettings()
                    {
                        GuildId = guild.Id,
                    });
                    guildLogSettingsToAdd.Add(new GuildLogSettings()
                    {
                        GuildId = guild.Id,
                    });
                    guildModerationSettingsToAdd.Add(new GuildModerationSettings()
                    {
                        GuildId = guild.Id,
                    });
                }
            }

            await this._guildRepository.AddMultipleAsync(guildsToAdd);
            await this._guildLevelRepository.AddMultipleAsync(guildLevelSettingsToAdd);
            await this._guildLogRepository.AddMultipleAsync(guildLogSettingsToAdd);
            await this._guildModerationRepository.AddMultipleAsync(guildModerationSettingsToAdd);
        }
    }
}
