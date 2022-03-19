// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Features.Shared.Commands.UpdateAllGuilds;
using Cybermancy.Core.Features.Shared.SharedDtos;
using DSharpPlus;
using DSharpPlus.EventArgs;
using MediatR;
using Nefarius.DSharpPlus.Extensions.Hosting.Attributes;
using Nefarius.DSharpPlus.Extensions.Hosting.Events;

namespace Cybermancy.DatabaseManagementModules
{
    [DiscordGuildEventsSubscriber]
    [DiscordChannelEventsSubscriber]
    public class GuildEventMangementModule : IDiscordGuildEventsSubscriber, IDiscordChannelEventsSubscriber
    {
        private readonly IMediator _mediator;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedManagementModule"/> class.
        /// </summary>
        /// <param name="guildService"></param>
        public GuildEventMangementModule(IMediator mediator)
        {
            this._mediator = mediator;
        }

        public Task DiscordOnGuildDownloadCompleted(DiscordClient sender, GuildDownloadCompletedEventArgs args)
            => this._mediator.Send(new UpdateAllGuildsQuery
            {
                Guilds = args.Guilds.Keys.Select(x => new GuildDto { Id = x }),
                Users = args.Guilds.Values.SelectMany(x => x.Members)
                    .DistinctBy(x => x.Value.Id).Select(x => x.Value)
                    .Select(x => new UserDto { AvatarUrl = x.AvatarUrl, Id = x.Id, UserName = $"{x.Username}#{x.Discriminator}" }),
                GuildUsers = args.Guilds.Values.SelectMany(x => x.Members)
                    .Select(x => x.Value).Select(x => new GuildUserDto { GuildId = x.Guild.Id, UserId = x.Id, DisplayName = x.DisplayName }),
                Roles = args.Guilds.Values.Select(x => new { x.Id, x.Roles })
                    .Select(x => x.Roles.Select(y => new RoleDto { GuildId = x.Id, Id = y.Value.Id }))
                    .SelectMany(x => x),
                Channels = args.Guilds.Values.SelectMany(x => x.Channels)
                    .Select(x => new ChannelDto { Id = x.Value.Id, GuildId = x.Value.GuildId.GetValueOrDefault(), Name = x.Value.Name })
            });

        
        public Task DiscordOnGuildCreated(DiscordClient sender, GuildCreateEventArgs args) => Task.CompletedTask;

        public Task DiscordOnGuildUpdated(DiscordClient sender, GuildUpdateEventArgs args) => Task.CompletedTask;

        public Task DiscordOnGuildDeleted(DiscordClient sender, GuildDeleteEventArgs args) => Task.CompletedTask;

        #region UnusedEvents
        public Task DiscordOnGuildAvailable(DiscordClient sender, GuildCreateEventArgs args) => Task.CompletedTask;
        public Task DiscordOnGuildEmojisUpdated(DiscordClient sender, GuildEmojisUpdateEventArgs args) => Task.CompletedTask;
        public Task DiscordOnGuildIntegrationsUpdated(DiscordClient sender, GuildIntegrationsUpdateEventArgs args) => Task.CompletedTask;
        public Task DiscordOnGuildStickersUpdated(DiscordClient sender, GuildStickersUpdateEventArgs args) => Task.CompletedTask;
        public Task DiscordOnGuildUnavailable(DiscordClient sender, GuildDeleteEventArgs args) => Task.CompletedTask;
        public Task DiscordOnChannelCreated(DiscordClient sender, ChannelCreateEventArgs args) => Task.CompletedTask;
        public Task DiscordOnChannelUpdated(DiscordClient sender, ChannelUpdateEventArgs args) => Task.CompletedTask;
        public Task DiscordOnChannelDeleted(DiscordClient sender, ChannelDeleteEventArgs args) => Task.CompletedTask;
        public Task DiscordOnDmChannelDeleted(DiscordClient sender, DmChannelDeleteEventArgs args) => Task.CompletedTask;
        public Task DiscordOnChannelPinsUpdated(DiscordClient sender, ChannelPinsUpdateEventArgs args) => Task.CompletedTask;
        #endregion
    }
}
