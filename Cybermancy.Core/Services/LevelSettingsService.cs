using System;
using System.Data;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Persistence;
using Cybermancy.Core.Contracts.Services;
using Cybermancy.Core.Enums;
using Cybermancy.Core.Utilities;
using Cybermancy.Domain;
using DSharpPlus.Entities;
using Nefarius.DSharpPlus.Extensions.Hosting;

namespace Cybermancy.Core.Services
{
    public class LevelSettingsService : ILevelSettingsService
    {
        private readonly IAsyncRepository<GuildLevelSettings> _guildLevelSettingsRepository;
        private readonly IAsyncIdRepository<Guild> _guildRepository;
        private readonly IDiscordClientService _discordClientService;

        public LevelSettingsService(IAsyncRepository<GuildLevelSettings> guildLevelSettingsRepository, IAsyncIdRepository<Guild> guildRepository, IDiscordClientService discordClientService)
        {
            _guildLevelSettingsRepository = guildLevelSettingsRepository;
            _guildRepository = guildRepository;
            _discordClientService = discordClientService;
        }

        public async Task SendLevelingLog(ulong guildId, CybermancyColor color, string message = null, string title = null,
            string footer = null, DiscordEmbed embed = null, DateTime? timeStamp = null)
        {
            var guild = await _guildRepository.GetByIdAsync(guildId);
            if (guild.LevelSettings.LevelChannelLog is null) return;
            DiscordChannel channel = null;
            try
            {
                channel = await _discordClientService.Client.GetChannelAsync(guild.LevelSettings.LevelChannelLog.Value);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            if (channel is null) return;
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

        public async Task<GuildLevelSettings> Update(GuildLevelSettings guildLevelSettings)
        {
            return await _guildLevelSettingsRepository.UpdateAsync(guildLevelSettings);
        }
    }
}