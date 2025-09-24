// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Moderation.Lock.Commands;

public sealed class UnlockChannel
{
    [RequireGuild]
    [RequireModuleEnabled(Module.Moderation)]
    [RequirePermissions([DiscordPermission.ManageChannels], [DiscordPermission.ManageMessages])]
    internal sealed class Command(IMediator mediator, GuildLog guildLog)
    {
        private readonly GuildLog _guildLog = guildLog;
        private readonly IMediator _mediator = mediator;

        [Command("Unlock")]
        [Description("Unlocks a channel.")]
        public async Task UnlockChannelAsync(
            SlashCommandContext ctx,
            [Parameter("Channel")] [Description("The channel to unlock. Current channel if not specified.")]
            DiscordChannel? channel = null)
        {
            await ctx.DeferResponseAsync();

            if (ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");

            channel ??= ctx.Channel;
            var response =
                await this._mediator.Send(new Request { ChannelId = channel.Id, GuildId = ctx.Guild.Id });

            if (!channel.IsThread)
            {
                var permissions = ctx.Guild.Channels[channel.Id].PermissionOverwrites
                    .First(x => x.Id == ctx.Guild.EveryoneRole.Id);
                await channel.AddOverwriteAsync(ctx.Guild.EveryoneRole,
                    permissions.Allowed.RevertLockPermissions(response.PreviouslyAllowed)
                    , permissions.Denied.RevertLockPermissions(response.PreviouslyDenied));
            }

            await ctx.EditReplyAsync(message: $"{channel.Mention} has been unlocked");

            await this._guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = ctx.Guild.Id,
                GuildLogType = GuildLogType.Moderation,
                Color = GrimoireColor.Purple,
                Description = $"{ctx.User.Mention} unlocked {channel.Mention}"
            });
        }
    }


    public sealed record Request : IRequest<Response>
    {
        public required ChannelId ChannelId { get; init; }
        public required GuildId GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Request command,
            CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbContext.Locks
                .Where(x => x.ChannelId == command.ChannelId && x.GuildId == command.GuildId)
                .Select(x => new { Lock = x, ModerationLogId = x.Guild.ModChannelLog })
                .FirstOrDefaultAsync(cancellationToken);
            if (result?.Lock is null)
                throw new AnticipatedException("Could not find a lock entry for that channel.");

            dbContext.Locks.Remove(result.Lock);
            await dbContext.SaveChangesAsync(cancellationToken);

            return new Response
            {
                PreviouslyAllowed = result.Lock.PreviouslyAllowed, PreviouslyDenied = result.Lock.PreviouslyDenied
            };
        }
    }

    public sealed record Response
    {
        public long PreviouslyAllowed { get; init; }
        public long PreviouslyDenied { get; init; }
    }
}
