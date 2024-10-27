// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Commands;

namespace Grimoire.Features.Shared;

internal sealed class ChannelEventManagementModule(IMediator mediator)
{
    private readonly IMediator _mediator = mediator;

    public async Task DiscordOnChannelCreated(DiscordClient sender, ChannelCreatedEventArgs args)
        => await this._mediator.Send(
            new AddChannelCommand { ChannelId = args.Channel.Id, GuildId = args.Guild.Id });

    public async Task DiscordOnChannelDeleted(DiscordClient sender, ChannelDeletedEventArgs args)
        => await this._mediator.Send(
            new DeleteChannelCommand { ChannelId = args.Channel.Id });

    public async Task DiscordOnThreadCreated(DiscordClient sender, ThreadCreatedEventArgs args)
        => await this._mediator.Send(
            new AddChannelCommand { ChannelId = args.Thread.Id, GuildId = args.Guild.Id });

    public async Task DiscordOnThreadDeleted(DiscordClient sender, ThreadDeletedEventArgs args)
        => await this._mediator.Send(
            new DeleteChannelCommand { ChannelId = args.Thread.Id });
}
