using System;
using System.Threading.Tasks;
using Cybermancy.Core.Enums;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Contracts.Services
{
    public interface ILevelSettingsService
    {
        Task SendLevelingLog(ulong guildId, CybermancyColor color, 
            string message = null, string title = null,
            string footer = null,
            DiscordEmbed embed = null,
            DateTime? timeStamp = null);
        Task<GuildLevelSettings> Update(GuildLevelSettings guildLevelSettings);
        Task<bool> IsLevelingEnabled(ulong guildId);
        Task<GuildLevelSettings> GetGuild(ulong guildId);
    }
}