using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Contracts.Services
{
    public interface IGuildService
    {
        Task<Guild> GetGuild(DiscordGuild guild);
        Task<Guild> GetGuild(ulong guildId);
        Task SetupAllGuild(IEnumerable<DiscordGuild> guilds);
    }
}