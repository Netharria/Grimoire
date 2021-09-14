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

        public GuildService(IAsyncIdRepository<Guild> guildRepository, IAsyncRepository<GuildLevelSettings> guildLevelRepository, 
            IAsyncRepository<GuildModerationSettings> guildModerationRepository, IAsyncRepository<GuildLogSettings> guildLogRepository)
        {
            _guildRepository = guildRepository;
            _guildLevelRepository = guildLevelRepository;
            _guildModerationRepository = guildModerationRepository;
            _guildLogRepository = guildLogRepository;
        }
        public async Task<Guild> GetGuild(DiscordGuild guild)
        {
            if (await _guildRepository.Exists(guild.Id)) return await _guildRepository.GetByIdAsync(guild.Id);
            await _guildRepository.AddAsync(new Guild()
            {
                Id = guild.Id
            });
            await _guildLevelRepository.AddAsync(new GuildLevelSettings()
            {
                GuildId = guild.Id
            });
            await _guildLogRepository.AddAsync(new GuildLogSettings()
            {
                GuildId = guild.Id
            });
            await _guildModerationRepository.AddAsync(new GuildModerationSettings()
            {
                GuildId = guild.Id
            });

            return await _guildRepository.GetByIdAsync(guild.Id);
        }

        public async Task<Guild> GetGuild(ulong guildId)
        {
            return await _guildRepository.GetByIdAsync(guildId);
        }

        public async Task SetupAllGuild(IEnumerable<DiscordGuild> guilds)
        {
            var guildsToAdd = new List<Guild>();
            var guildLevelSettingsToAdd = new List<GuildLevelSettings>();
            var guildLogSettingsToAdd = new List<GuildLogSettings>();
            var guildModerationSettingsToAdd = new List<GuildModerationSettings>();
            foreach(var guild in guilds.Where(x => !_guildRepository.Exists(x.Id).Result))
            {
                guildsToAdd.Add(new Guild()
                {
                    Id = guild.Id
                });
                guildLevelSettingsToAdd.Add(new GuildLevelSettings()
                {
                    GuildId = guild.Id
                });
                guildLogSettingsToAdd.Add(new GuildLogSettings()
                {
                    GuildId = guild.Id
                });
                guildModerationSettingsToAdd.Add(new GuildModerationSettings()
                {
                    GuildId = guild.Id
                });
            }

            await _guildRepository.AddMultipleAsync(guildsToAdd);
            await _guildLevelRepository.AddMultipleAsync(guildLevelSettingsToAdd);
            await _guildLogRepository.AddMultipleAsync(guildLogSettingsToAdd);
            await _guildModerationRepository.AddMultipleAsync(guildModerationSettingsToAdd);
        }
    }
}