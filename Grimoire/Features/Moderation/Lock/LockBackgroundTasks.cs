// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Grimoire.Features.Moderation.Lock.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Moderation.Lock;

internal sealed class LockBackgroundTasks(IServiceProvider serviceProvider, ILogger<LockBackgroundTasks> logger)
    : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromSeconds(5))
{
    protected override async Task RunTask(IServiceProvider serviceProvider, CancellationToken stoppingToken)
    {
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var discordClient = serviceProvider.GetRequiredService<DiscordClient>();

        await foreach (var expiredLock in mediator.CreateStream(new GetExpiredLocks.Request(), stoppingToken))
        {
            var guild = discordClient.Guilds.GetValueOrDefault(expiredLock.GuildId);
            if (guild is null)
                continue;

            var channel = guild.Channels.GetValueOrDefault(expiredLock.ChannelId);
            channel ??= guild.Threads.GetValueOrDefault(expiredLock.ChannelId);

            if (channel is null)
                continue;

            if (!channel.IsThread)
            {
                var permissions = channel.PermissionOverwrites.First(x => x.Id == guild.EveryoneRole.Id);
                await channel.AddOverwriteAsync(guild.EveryoneRole,
                    permissions.Allowed.RevertLockPermissions(expiredLock.PreviouslyAllowed)
                    , permissions.Denied.RevertLockPermissions(expiredLock.PreviouslyDenied));
            }

            _ = await mediator.Send(new UnlockChannel.Request { ChannelId = channel.Id, GuildId = guild.Id },
                stoppingToken);

            var embed = new DiscordEmbedBuilder()
                .WithDescription($"Lock on {channel.Mention} has expired.");

            await channel.SendMessageAsync(embed);
            await discordClient.SendMessageToLoggingChannel(expiredLock.LogChannelId,
                builder => builder.AddEmbed(embed));
        }
    }
}

public sealed class GetExpiredLocks
{
    public sealed record Request : IStreamRequest<Response>;

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IStreamRequestHandler<Request, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async IAsyncEnumerable<Response> Handle(Request query,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            await foreach (var lockedChannel in dbContext.Locks
                               .AsNoTracking()
                               .Where(x => x.EndTime < DateTimeOffset.UtcNow)
                               .Select(x => new Response
                               {
                                   ChannelId = x.ChannelId,
                                   GuildId = x.GuildId,
                                   PreviouslyAllowed = x.PreviouslyAllowed,
                                   PreviouslyDenied = x.PreviouslyDenied,
                                   LogChannelId = x.Guild.ModChannelLog
                               }).AsAsyncEnumerable().WithCancellation(cancellationToken))
                yield return lockedChannel;
        }
    }

    public sealed record Response : BaseResponse
    {
        public ulong ChannelId { get; init; }
        public ulong GuildId { get; init; }
        public long PreviouslyAllowed { get; init; }
        public long PreviouslyDenied { get; init; }
    }
}
