using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Persistence;
using Cybermancy.Core.Contracts.Services;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Services
{
    public class ChannelService: IChannelService
    {
        private readonly IAsyncIdRepository<Channel> _channelRepository;
        
        public ChannelService(IAsyncIdRepository<Channel> channelRepository)
        {
            _channelRepository = channelRepository;
        }

        public async Task<Channel> GetChannel(ulong channelId)
        {
            return await _channelRepository.GetByIdAsync(channelId);
        }

        public async Task<Channel> GetChannel(DiscordChannel discordChannel)
        {
            if (await _channelRepository.Exists(discordChannel.Id))
                return await _channelRepository.GetByIdAsync(discordChannel.Id);
            else
            {
                var newChannel = new Channel()
                {
                    Id = discordChannel.Id,
                    GuildId = discordChannel.GuildId.Value,
                    Name = discordChannel.Name
                };
                await Save(newChannel);
            }

            return await _channelRepository.GetByIdAsync(discordChannel.Id);
        }

        public async Task<bool> IsChannelIgnored(DiscordChannel discordChannel)
        {
            if (await _channelRepository.Exists(discordChannel.Id))
                return (await _channelRepository.GetByIdAsync(discordChannel.Id)).IsXpIgnored ;
            else
            {
                var newChannel = new Channel()
                {
                    Id = discordChannel.Id,
                    GuildId = discordChannel.GuildId.Value,
                    Name = discordChannel.Name
                };
                await Save(newChannel);
            }

            return (await _channelRepository.GetByIdAsync(discordChannel.Id)).IsXpIgnored;
        }

        public async Task<Channel> Save(Channel channel)
        {
            if (await _channelRepository.Exists(channel.Id))
                return await _channelRepository.UpdateAsync(channel);
            return await _channelRepository.AddAsync(channel);
        }

        public async Task SetupAllChannels(IEnumerable<DiscordGuild> guilds)
        {
            var newChannels = new List<Channel>();
            foreach(var guild in guilds)
            {
                foreach (var channel in guild.Channels.Values.Where(x => _channelRepository.Exists(x.Id).Result))
                {
                    newChannels.Add(new Channel()
                    {
                        Id = channel.Id,
                        GuildId = channel.GuildId.Value,
                        Name = channel.Name
                    });
                }
            }
            await _channelRepository.AddMultipleAsync(newChannels);
        }
    }
}