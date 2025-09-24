// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using EntityFramework.Exceptions.Common;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Shared.Events;

internal sealed partial class ChannelAdded(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    ILogger<ChannelAdded> logger)
    : IEventHandler<ChannelCreatedEventArgs>,
        IEventHandler<ThreadCreatedEventArgs>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<ChannelAdded> _logger = logger;

    public Task HandleEventAsync(DiscordClient sender, ChannelCreatedEventArgs eventArgs)
        => AddChannel(eventArgs.Channel.Id, eventArgs.Guild.Id);

    public Task HandleEventAsync(DiscordClient sender, ThreadCreatedEventArgs eventArgs)
        => AddChannel(eventArgs.Thread.Id, eventArgs.Guild.Id);

    private async Task AddChannel(ulong channelId, ulong? guildId, CancellationToken cancellationToken = default)
    {
        if (guildId is null)
            return;
        var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        try
        {
            await dbContext.Channels.AddAsync(
                new Channel { Id = channelId, GuildId = guildId.Value }, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            LogFailedToAddChannel(this._logger, channelId, guildId.Value);
        }
    }

    [LoggerMessage(LogLevel.Debug, "Failed to add channel {channelId} for guild {guildId} to the database")]
    private static partial void LogFailedToAddChannel(ILogger logger, ulong channelId, ulong guildId);
}
