// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Shared.Commands;

namespace Grimoire.Discord.DatabaseManagementModules;

/// <summary>
/// Initializes a new instance of the <see cref="SharedManagementModule"/> class.
/// </summary>
/// <param name="guildService"></param>
[DiscordChannelCreatedEventSubscriber]
[DiscordChannelDeletedEventSubscriber]
[DiscordThreadCreatedEventSubscriber]
[DiscordThreadDeletedEventSubscriber]
public class ChannelEventManagementModule(IMediator mediator) :
    IDiscordChannelCreatedEventSubscriber,
    IDiscordChannelDeletedEventSubscriber,
    IDiscordThreadCreatedEventSubscriber,
    IDiscordThreadDeletedEventSubscriber
{
    private readonly IMediator _mediator = mediator;

    public async Task DiscordOnChannelCreated(DiscordClient sender, ChannelCreateEventArgs args)
        => await this._mediator.Send(
            new AddChannelCommand
            {
                ChannelId = args.Channel.Id,
                GuildId = args.Guild.Id
            });

    public async Task DiscordOnChannelDeleted(DiscordClient sender, ChannelDeleteEventArgs args)
        => await this._mediator.Send(
            new DeleteChannelCommand
            {
                ChannelId = args.Channel.Id
            });

    public async Task DiscordOnThreadCreated(DiscordClient sender, ThreadCreateEventArgs args)
        => await this._mediator.Send(
            new AddChannelCommand
            {
                ChannelId = args.Thread.Id,
                GuildId = args.Guild.Id
            });
    public async Task DiscordOnThreadDeleted(DiscordClient sender, ThreadDeleteEventArgs args)
        => await this._mediator.Send(
            new DeleteChannelCommand
            {
                ChannelId = args.Thread.Id
            });
}
