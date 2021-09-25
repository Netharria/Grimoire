// -----------------------------------------------------------------------
// <copyright file="SharedManagementModule.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Services;
using DSharpPlus;
using DSharpPlus.EventArgs;
using Nefarius.DSharpPlus.Extensions.Hosting.Attributes;
using Nefarius.DSharpPlus.Extensions.Hosting.Events;

namespace Cybermancy.Core
{
    [DiscordWebSocketEventSubscriber]
    public class SharedManagementModule : IDiscordWebSocketEventSubscriber
    {
        private readonly IGuildService _guildService;
        private readonly IRoleService _roleService;
        private readonly IChannelService _channelService;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedManagementModule"/> class.
        /// </summary>
        /// <param name="guildService"></param>
        /// <param name="roleService"></param>
        /// <param name="channelService"></param>
        public SharedManagementModule(IGuildService guildService, IRoleService roleService, IChannelService channelService)
        {
            this._guildService = guildService;
            this._roleService = roleService;
            this._channelService = channelService;
        }

        public async Task DiscordOnReady(DiscordClient sender, ReadyEventArgs args)
        {
            await Task.Run(() => this._guildService.SetupAllGuildAsync(sender.Guilds.Values));
            await Task.Run(() => this._roleService.SetupAllRolesAsync(sender.Guilds.Values));
            await Task.Run(() => this._channelService.SetupAllChannelsAsync(sender.Guilds.Values));
        }

        #region UnusedEvents

        public Task DiscordOnHeartbeated(DiscordClient sender, HeartbeatEventArgs args) => Task.CompletedTask;

        public Task DiscordOnResumed(DiscordClient sender, ReadyEventArgs args) => Task.CompletedTask;

        public Task DiscordOnSocketClosed(DiscordClient sender, SocketCloseEventArgs args) => Task.CompletedTask;

        public Task DiscordOnSocketOpened(DiscordClient sender, SocketEventArgs args) => Task.CompletedTask;

        public Task DiscordOnZombied(DiscordClient sender, ZombiedEventArgs args) => Task.CompletedTask;

        public Task DiscordOSocketErrored(DiscordClient sender, SocketErrorEventArgs args) => Task.CompletedTask;

        #endregion
    }
}
