// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Features.Shared.Commands.ChannelCommands.AddChannel;
using Cybermancy.Core.Features.Shared.Commands.ChannelCommands.DeleteChannel;
using Cybermancy.Core.Features.Shared.Commands.ChannelCommands.UpdateChannel;
using DSharpPlus;
using DSharpPlus.EventArgs;
using MediatR;
using Nefarius.DSharpPlus.Extensions.Hosting.Events;

namespace Cybermancy.DatabaseManagementModules
{
    [DiscordChannelCreatedEventSubscriber]
    [DiscordChannelDeletedEventSubscriber]
    [DiscordChannelUpdatedEventSubscriber]
    public class ChannelEventManagementModule :
        IDiscordChannelCreatedEventSubscriber,
        IDiscordChannelDeletedEventSubscriber,
        IDiscordChannelUpdatedEventSubscriber
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
                    ChannelName = args.Channel.Name,
                    GuildId = args.Guild.Id
                });

        public Task DiscordOnChannelDeleted(DiscordClient sender, ChannelDeleteEventArgs args)
            => this._mediator.Send(
                new DeleteChannelCommand
                {
                    ChannelId = args.Channel.Id
                });

        public Task DiscordOnChannelUpdated(DiscordClient sender, ChannelUpdateEventArgs args)
            => this._mediator.Send(
                new UpdateChannelCommand
                {
                    ChannelId = args.ChannelAfter.Id,
                    ChannelName = args.ChannelAfter.Name,
                    GuildId = args.Guild.Id
                });
    }
}
