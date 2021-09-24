// -----------------------------------------------------------------------
// <copyright file="GuildService.cs" company="Netharia">
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

    public class GuildService : IGuildService
    {
        private readonly IAsyncIdRepository<Guild> guildRepository;
        private readonly IAsyncRepository<GuildLevelSettings> guildLevelRepository;
        private readonly IAsyncRepository<GuildModerationSettings> guildModerationRepository;
        private readonly IAsyncRepository<GuildLogSettings> guildLogRepository;

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
            this.guildRepository = guildRepository;
            this.guildLevelRepository = guildLevelRepository;
            this.guildModerationRepository = guildModerationRepository;
            this.guildLogRepository = guildLogRepository;
        }

        public async ValueTask<Guild> GetGuildAsync(DiscordGuild guild)
        {
            if (await this.guildRepository.ExistsAsync(guild.Id)) return await this.guildRepository.GetByIdAsync(guild.Id);
            await this.guildRepository.AddAsync(new Guild()
            {
                Id = guild.Id,
            });
            await this.guildLevelRepository.AddAsync(new GuildLevelSettings()
            {
                GuildId = guild.Id,
            });
            await this.guildLogRepository.AddAsync(new GuildLogSettings()
            {
                GuildId = guild.Id,
            });
            await this.guildModerationRepository.AddAsync(new GuildModerationSettings()
            {
                GuildId = guild.Id,
            });

            return await this.guildRepository.GetByIdAsync(guild.Id);
        }

        public ValueTask<Guild> GetGuildAsync(ulong guildId)
        {
            return this.guildRepository.GetByIdAsync(guildId);
        }

        public async Task SetupAllGuildAsync(IEnumerable<DiscordGuild> guilds)
        {
            var guildsToAdd = new List<Guild>();
            var guildLevelSettingsToAdd = new List<GuildLevelSettings>();
            var guildLogSettingsToAdd = new List<GuildLogSettings>();
            var guildModerationSettingsToAdd = new List<GuildModerationSettings>();
            foreach (var guild in guilds)
            {
                if (!await this.guildRepository.ExistsAsync(guild.Id))
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

            await this.guildRepository.AddMultipleAsync(guildsToAdd);
            await this.guildLevelRepository.AddMultipleAsync(guildLevelSettingsToAdd);
            await this.guildLogRepository.AddMultipleAsync(guildLogSettingsToAdd);
            await this.guildModerationRepository.AddMultipleAsync(guildModerationSettingsToAdd);
        }
    }
}