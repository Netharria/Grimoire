// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Events;

public sealed record ChannelDeleted(IDbContextFactory<GrimoireDbContext> dbContextFactory)
    : IEventHandler<ChannelDeletedEventArgs>,
        IEventHandler<ThreadDeletedEventArgs>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

    public Task HandleEventAsync(DiscordClient sender, ChannelDeletedEventArgs eventArgs)
        => DeleteChannel(eventArgs.Channel.Id);

    public Task HandleEventAsync(DiscordClient sender, ThreadDeletedEventArgs eventArgs)
        => DeleteChannel(eventArgs.Thread.Id);

    private async Task DeleteChannel(ulong channelId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Channels
            .Where(channel => channel.Id == channelId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
