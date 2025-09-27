// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using DSharpPlus.Exceptions;
using Grimoire.Features.Moderation.Mute.Commands;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Moderation.Mute;

internal sealed class MuteBackgroundTasks(IServiceProvider serviceProvider, ILogger<MuteBackgroundTasks> logger)
    : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromSeconds(5))
{
    protected override async Task RunTask(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var discordClient = serviceProvider.GetRequiredService<DiscordClient>();
        var guildLog = serviceProvider.GetRequiredService<GuildLog>();

        await foreach (var expiredLock in mediator.CreateStream(new GetExpiredMutes.Query(), cancellationToken))
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
                await guildLog.SendLogMessageAsync(
                    new GuildLogMessage
                    {
                        GuildId = guild.Id,
                        GuildLogType = GuildLogType.Moderation,
                        Description =
                            $"Tried to unmute {user.Mention} but was unable to. Please remove the mute role manually."
                    }, cancellationToken);
            }

            _ = await mediator.Send(new UnmuteUser.Request { UserId = user.Id, GuildId = guild.Id }, cancellationToken);

            var embed = new DiscordEmbedBuilder()
                .WithDescription($"Mute on {user.Mention} has expired.");

            await user.SendMessageAsync(embed);

            await guildLog.SendLogMessageAsync(
                new GuildLogMessage
                {
                    GuildId = guild.Id,
                    GuildLogType = GuildLogType.Moderation,
                    Description = $"Mute on {user.Mention} has expired."
                }, cancellationToken);
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
                                   MuteRole = x.Guild.ModerationSettings.MuteRole!.Value
                               }).AsAsyncEnumerable()
                               .WithCancellation(cancellationToken))
                yield return mute;
        }
    }

    public sealed record Response
    {
        public ulong UserId { get; init; }
        public GuildId GuildId { get; init; }
        public ulong MuteRole { get; init; }
    }
}
