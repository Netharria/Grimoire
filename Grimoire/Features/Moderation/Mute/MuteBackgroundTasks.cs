// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using DSharpPlus.Exceptions;
using Grimoire.Features.Moderation.Mute.Commands;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Moderation.Mute;

internal sealed class MuteBackgroundTasks(IServiceProvider serviceProvider, ILogger<MuteBackgroundTasks> logger)
    : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromSeconds(5))
{
    protected override async Task RunTask(IServiceProvider serviceProvider, CancellationToken stoppingToken)
    {
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var discordClient = serviceProvider.GetRequiredService<DiscordClient>();

        await foreach (var expiredLock in mediator.CreateStream(new GetExpiredMutes.Query(), stoppingToken))
        {
            var guild = discordClient.Guilds.GetValueOrDefault(expiredLock.GuildId);
            if (guild is null) continue;

            var user = guild.Members.GetValueOrDefault(expiredLock.UserId);

            if (user is null) continue;
            var role = guild.Roles.GetValueOrDefault(expiredLock.MuteRole);
            if (role is null) continue;
            try
            {
                await user.RevokeRoleAsync(role);
            }
            catch (DiscordException)
            {
                if (expiredLock.LogChannelId is not null)
                {
                    var moderationLogChannel = guild.Channels.GetValueOrDefault(expiredLock.LogChannelId.Value);
                    if (moderationLogChannel is not null)
                        await moderationLogChannel.SendMessageAsync(new DiscordEmbedBuilder()
                            .WithDescription(
                                $"Tried to unmute {user.Mention} but was unable to. Please remove the mute role manually."));
                }
            }

            _ = await mediator.Send(new UnmuteUser.Request { UserId = user.Id, GuildId = guild.Id }, stoppingToken);

            var embed = new DiscordEmbedBuilder()
                .WithDescription($"Mute on {user.Mention} has expired.");

            await user.SendMessageAsync(embed);


            if (expiredLock.LogChannelId is not null)
            {
                var moderationLogChannel = guild.Channels.GetValueOrDefault(expiredLock.LogChannelId.Value);
                if (moderationLogChannel is not null)
                    await moderationLogChannel.SendMessageAsync(embed);
            }
        }
    }
}

public sealed class GetExpiredMutes
{
    public sealed record Query : IStreamRequest<Response>;

    [UsedImplicitly]
    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IStreamRequestHandler<Query, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async IAsyncEnumerable<Response> Handle(Query query,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            await foreach (var mute in dbContext.Mutes
                               .AsNoTracking()
                               .Where(x => x.EndTime < DateTimeOffset.UtcNow)
                               .Where(x => x.Guild.ModerationSettings.MuteRole != null)
                               .Select(x => new Response
                               {
                                   UserId = x.UserId,
                                   GuildId = x.GuildId,
                                   MuteRole = x.Guild.ModerationSettings.MuteRole!.Value,
                                   LogChannelId = x.Guild.ModChannelLog
                               }).AsAsyncEnumerable().WithCancellation(cancellationToken))
                yield return mute;
        }
    }

    public sealed record Response : BaseResponse
    {
        public ulong UserId { get; init; }
        public ulong GuildId { get; init; }
        public ulong MuteRole { get; init; }
    }
}
