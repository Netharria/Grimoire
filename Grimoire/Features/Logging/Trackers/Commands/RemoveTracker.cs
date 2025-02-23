// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Logging.Trackers.Commands;

public sealed class RemoveTracker
{
    [RequireGuild]
    [RequireModuleEnabled(Module.MessageLog)]
    [RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
    internal sealed class Command(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        [SlashCommand("Untrack", "Stops the logging of the user's activity")]
        public async Task UnTrackAsync(InteractionContext ctx,
            [Option("User", "User to stop logging.")]
            DiscordUser member)
        {
            await ctx.DeferAsync();
            var response = await this._mediator.Send(new Request { UserId = member.Id, GuildId = ctx.Guild.Id });


            await ctx.EditReplyAsync(message: $"Tracker removed from {member.Mention}");

            await ctx.Client.SendMessageToLoggingChannel(response.ModerationLogId,
                embed => embed
                    .WithDescription(
                        $"{ctx.Member.GetUsernameWithDiscriminator()} removed a tracker on {member.Mention}")
                    .WithColor(GrimoireColor.Purple));
        }
    }

    public sealed record Request : IRequest<Response>
    {
        public ulong UserId { get; init; }
        public ulong GuildId { get; init; }
    }

    public sealed class RemoveTrackerCommandHandler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Request command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbContext.Trackers
                .Where(x => x.UserId == command.UserId && x.GuildId == command.GuildId)
                .Select(x => new
                {
                    Tracker = x, ModerationLogId = x.Guild.ModChannelLog, TrackerChannelId = x.LogChannelId
                }).FirstOrDefaultAsync(cancellationToken);
            if (result?.Tracker is null)
                throw new AnticipatedException("Could not find a tracker for that user.");

            dbContext.Trackers.Remove(result.Tracker);
            await dbContext.SaveChangesAsync(cancellationToken);

            return new Response
            {
                ModerationLogId = result.ModerationLogId, TrackerChannelId = result.TrackerChannelId
            };
        }
    }

    public sealed record Response : BaseResponse
    {
        public ulong? ModerationLogId { get; init; }
        public ulong TrackerChannelId { get; init; }
    }
}
