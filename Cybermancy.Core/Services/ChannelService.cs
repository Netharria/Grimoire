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
        private readonly IGuildService _guildService;
        
        public ChannelService(IAsyncIdRepository<Channel> channelRepository, IGuildService guildService)
        {
            _channelRepository = channelRepository;
            _guildService = guildService;
        }
        public async Task<bool> IsChannelIgnored(DiscordChannel channel)
        {
            Channel databaseChannel;
            if (await _channelRepository.Exists(channel.Id))
            {
                databaseChannel = await _channelRepository.GetByIdAsync(channel.Id);
            }
            else
            {
                var newChannel = new Channel()
                {
                    Id = channel.Id,
                    Guild = await _guildService.GetGuildAndSetupIfDoesntExist(channel.Guild),
                    Name = channel.Name
                };
                databaseChannel = await Save(newChannel);
            }

            return databaseChannel.IsXpIgnored;
        }

        public async Task<Channel> Save(Channel channel)
        {
            if (await _channelRepository.Exists(channel.Id))
                return await _channelRepository.UpdateAsync(channel);
            return await _channelRepository.AddAsync(channel);
        }
    }
}