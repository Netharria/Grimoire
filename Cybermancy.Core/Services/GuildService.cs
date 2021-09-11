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
        public GuildService(IAsyncIdRepository<Guild> guildRepository, IAsyncRepository<GuildLevelSettings> guildLevelRepository, IAsyncRepository<GuildModerationSettings> guildModerationRepository, IAsyncRepository<GuildLogSettings> guildLogRepository)
        {
            _guildRepository = guildRepository;
            _guildLevelRepository = guildLevelRepository;
            _guildModerationRepository = guildModerationRepository;
            _guildLogRepository = guildLogRepository;
        }
        public async Task<Guild> GetGuildAndSetupIfDoesntExist(DiscordGuild guild)
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
    }
}