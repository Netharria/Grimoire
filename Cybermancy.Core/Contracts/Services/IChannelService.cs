using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Contracts.Services
{
    public interface IChannelService
    {
        Task<bool> IsChannelIgnored(DiscordChannel discordChannel);
        Task<ICollection<Channel>> GetAllIgnoredChannels(ulong guildId);
        Task<Channel> Save(Channel channel);
        Task SetupAllChannels(IEnumerable<DiscordGuild> guilds);
        Task<Channel> GetChannel(ulong id);
        Task<Channel> GetChannel(DiscordChannel discordChannel);
    }
}