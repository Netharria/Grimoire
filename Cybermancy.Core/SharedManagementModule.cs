using Cybermancy.Core.Contracts.Services;
using DSharpPlus;
using DSharpPlus.EventArgs;
using Nefarius.DSharpPlus.Extensions.Hosting.Attributes;
using Nefarius.DSharpPlus.Extensions.Hosting.Events;
using System.Threading.Tasks;

namespace Cybermancy.Core
{
    [DiscordWebSocketEventSubscriber]
    public class SharedManagementModule : IDiscordWebSocketEventSubscriber
    {
        private readonly IGuildService _guildService;
        private readonly IRoleService _roleService;
        private readonly IChannelService _channelService;

        public SharedManagementModule(IGuildService guildService, IRoleService roleService, IChannelService channelService)
        {
            _guildService = guildService;
            _roleService = roleService;
            _channelService = channelService;
        }

        public Task DiscordOnReady(DiscordClient sender, ReadyEventArgs args)
        {
            Task.Run(() =>  _guildService.SetupAllGuild(sender.Guilds.Values));
            Task.Run(() => _roleService.SetupAllRoles(sender.Guilds.Values));
            Task.Run(() => _channelService.SetupAllChannels(sender.Guilds.Values));
            return Task.CompletedTask;
        }

        #region UnusedEvents

        public Task DiscordOnHeartbeated(DiscordClient sender, HeartbeatEventArgs args)
        {
            return Task.CompletedTask;
        }


        public Task DiscordOnResumed(DiscordClient sender, ReadyEventArgs args)
        {
            return Task.CompletedTask;
        }

        public Task DiscordOnSocketClosed(DiscordClient sender, SocketCloseEventArgs args)
        {
            return Task.CompletedTask;
        }

        public Task DiscordOnSocketOpened(DiscordClient sender, SocketEventArgs args)
        {
            return Task.CompletedTask;
        }

        public Task DiscordOnZombied(DiscordClient sender, ZombiedEventArgs args)
        {
            return Task.CompletedTask;
        }

        public Task DiscordOSocketErrored(DiscordClient sender, SocketErrorEventArgs args)
        {
            return Task.CompletedTask;
        }

        #endregion
    }
}
