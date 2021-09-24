// -----------------------------------------------------------------------
// <copyright file="LevelSettingsService.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Core.Services
{
    using System;
    using System.Threading.Tasks;
    using Cybermancy.Core.Contracts.Persistence;
    using Cybermancy.Core.Contracts.Services;
    using Cybermancy.Core.Enums;
    using Cybermancy.Core.Utilities;
    using Cybermancy.Domain;
    using DSharpPlus.Entities;
    using Nefarius.DSharpPlus.Extensions.Hosting;

    public class LevelSettingsService : ILevelSettingsService
    {
        private readonly IAsyncRepository<GuildLevelSettings> guildLevelSettingsRepository;
        private readonly IAsyncIdRepository<Guild> guildRepository;
        private readonly IDiscordClientService discordClientService;

        /// <summary>
        /// Initializes a new instance of the <see cref="LevelSettingsService"/> class.
        /// </summary>
        /// <param name="guildLevelSettingsRepository"></param>
        /// <param name="guildRepository"></param>
        /// <param name="discordClientService"></param>
        public LevelSettingsService(
            IAsyncRepository<GuildLevelSettings> guildLevelSettingsRepository,
            IAsyncIdRepository<Guild> guildRepository,
            IDiscordClientService discordClientService)
        {
            this.guildLevelSettingsRepository = guildLevelSettingsRepository;
            this.guildRepository = guildRepository;
            this.discordClientService = discordClientService;
        }

        public async Task SendLevelingLogAsync(
            ulong guildId,
            CybermancyColor color,
            string message = null,
            string title = null,
            string footer = null,
            DiscordEmbed embed = null,
            DateTime? timeStamp = null)
        {
            var guild = await this.guildRepository.GetByIdAsync(guildId);
            if (guild.LevelSettings.LevelChannelLog is null) return;
            DiscordChannel channel = null;
            try
            {
                channel = await this.discordClientService.Client.GetChannelAsync(guild.LevelSettings.LevelChannelLog.Value);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            if (channel is null)
                return;
            timeStamp ??= DateTime.UtcNow;
            embed ??= new DiscordEmbedBuilder()
                .WithColor(ColorUtility.GetColor(color))
                .WithTitle(title)
                .WithDescription(message)
                .WithFooter(footer)
                .WithTimestamp(timeStamp)
                .Build();
            try
            {
                await channel.SendMessageAsync(embed);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public Task<GuildLevelSettings> UpdateAsync(GuildLevelSettings guildLevelSettings)
        {
            return this.guildLevelSettingsRepository.UpdateAsync(guildLevelSettings);
        }

        public async Task<bool> IsLevelingEnabledAsync(ulong guildId)
        {
            var guildLevelSettings = await this.guildLevelSettingsRepository.GetByPrimaryKeyAsync(guildId);
            return guildLevelSettings.IsLevelingEnabled;
        }

        public ValueTask<GuildLevelSettings> GetGuildAsync(ulong guildId)
        {
            return this.guildLevelSettingsRepository.GetByPrimaryKeyAsync(guildId);
        }
    }
}