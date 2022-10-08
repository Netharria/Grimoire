// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Features.Shared.Commands.ChannelCommands.AddChannel;
using Cybermancy.Core.Features.Shared.Commands.ChannelCommands.DeleteChannel;
using DSharpPlus;
using DSharpPlus.EventArgs;
using MediatR;
using Nefarius.DSharpPlus.Extensions.Hosting.Events;

namespace Cybermancy.Discord.DatabaseManagementModules
{
    [DiscordChannelCreatedEventSubscriber]
    [DiscordChannelDeletedEventSubscriber]
    [DiscordThreadCreatedEventSubscriber]
    [DiscordThreadDeletedEventSubscriber]
    public class ChannelEventManagementModule :
        IDiscordChannelCreatedEventSubscriber,
        IDiscordChannelDeletedEventSubscriber,
        IDiscordThreadCreatedEventSubscriber,
        IDiscordThreadDeletedEventSubscriber
    {
        private readonly IMediator _mediator;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedManagementModule"/> class.
        /// </summary>
        /// <param name="guildService"></param>
        public ChannelEventManagementModule(IMediator mediator)
        {
            this._mediator = mediator;
        }

        public Task DiscordOnChannelCreated(DiscordClient sender, ChannelCreateEventArgs args)
            => this._mediator.Send(
                new AddChannelCommand
                {
                    ChannelId = args.Channel.Id,
                    GuildId = args.Guild.Id
                });

        public Task DiscordOnChannelDeleted(DiscordClient sender, ChannelDeleteEventArgs args)
            => this._mediator.Send(
                new DeleteChannelCommand
                {
                    ChannelId = args.Channel.Id
                });

        public Task DiscordOnThreadCreated(DiscordClient sender, ThreadCreateEventArgs args)
            => this._mediator.Send(
                new AddChannelCommand
                {
                    ChannelId = args.Thread.Id,
                    GuildId = args.Guild.Id
                });
        public Task DiscordOnThreadDeleted(DiscordClient sender, ThreadDeleteEventArgs args)
            => this._mediator.Send(
                new DeleteChannelCommand
                {
                    ChannelId = args.Thread.Id
                });
    }
}
