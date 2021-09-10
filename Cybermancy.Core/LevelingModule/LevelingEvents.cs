using System.Linq;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Persistence;
using Cybermancy.Domain;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Nefarius.DSharpPlus.Extensions.Hosting.Attributes;
using Nefarius.DSharpPlus.Extensions.Hosting.Events;

namespace Cybermancy.Core.LevelingModule
{
    [DiscordMessageEventsSubscriber]
    public class LevelingEvents : IDiscordMessageEventsSubscriber
    {
        private readonly IAsyncIdRepository<Channel> _channelRepo;
        private readonly IAsyncIdRepository<Role> _roleRepo;

        public LevelingEvents(IAsyncIdRepository<Channel> channelRepo, IAsyncIdRepository<Role> roleRepo)
        {
            _channelRepo = channelRepo;
            _roleRepo = roleRepo;
        }

        public async Task DiscordOnMessageCreated(DiscordClient sender, MessageCreateEventArgs args)
        {
            if(args.Message.MessageType is not MessageType.Default or MessageType.Reply) return; 
            if (args.Author is not DiscordMember member) return;
            if(member.IsBot) return;
            var channel = await _channelRepo.GetByIdAsync(args.Channel.Id);
            if (channel is null) return;
            var roles = member.Roles
                .Select(async x => await _roleRepo.GetByIdAsync(x.Id))
                .Where(x => x.Result.IsXpIgnored).ToList();
            if(roles.Any()) return;
            
        }

        #region UnusedEvents
        public Task DiscordOnMessageAcknowledged(DiscordClient sender, MessageAcknowledgeEventArgs args)
        {
            return Task.CompletedTask;
        }

        public Task DiscordOnMessageUpdated(DiscordClient sender, MessageUpdateEventArgs args)
        {
            return Task.CompletedTask;
        }

        public Task DiscordOnMessageDeleted(DiscordClient sender, MessageDeleteEventArgs args)
        {
            return Task.CompletedTask;
        }

        public Task DiscordOnMessagesBulkDeleted(DiscordClient sender, MessageBulkDeleteEventArgs args)
        {
            return Task.CompletedTask;
        }
        #endregion
    }
}