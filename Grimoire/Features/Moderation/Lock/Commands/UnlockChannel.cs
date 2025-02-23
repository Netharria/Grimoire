// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Lock.Commands;

public sealed class UnlockChannel
{
    [RequireGuild]
    [RequireModuleEnabled(Module.Moderation)]
    [SlashRequireUserPermissions(false, DiscordPermission.ManageMessages)]
    [SlashRequireBotPermissions(false, DiscordPermission.ManageChannels)]
    internal sealed class Command(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        [SlashCommand("Unlock", "Prevents users from being able to speak in the channel")]
        public async Task UnlockChannelAsync(
            InteractionContext ctx,
            [Option("Channel", "The Channel to unlock. Current channel if not specified.")]
            DiscordChannel? channel = null)
        {
            await ctx.DeferAsync();
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

            await ctx.SendLogAsync(response, GrimoireColor.Purple,
                message: $"{channel.Mention} has been unlocked by {ctx.User.Mention}");
        }
    }


    public sealed record Request : IRequest<Response>
    {
        public required ulong ChannelId { get; init; }
        public required ulong GuildId { get; init; }
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
                LogChannelId = result.ModerationLogId,
                PreviouslyAllowed = result.Lock.PreviouslyAllowed,
                PreviouslyDenied = result.Lock.PreviouslyDenied
            };
        }
    }

    public sealed record Response : BaseResponse
    {
        public long PreviouslyAllowed { get; init; }
        public long PreviouslyDenied { get; init; }
    }
}
