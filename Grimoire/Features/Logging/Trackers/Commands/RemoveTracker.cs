// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Features.Shared.Channels.TrackerLog;

namespace Grimoire.Features.Logging.Trackers.Commands;

public sealed class RemoveTracker
{
    [RequireGuild]
    [RequireModuleEnabled(Module.MessageLog)]
    [RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
    internal sealed class Command(IMediator mediator, GuildLog guildLog, TrackerLog trackerLog)
    {
        private readonly GuildLog _guildLog = guildLog;
        private readonly IMediator _mediator = mediator;
        private readonly TrackerLog _trackerLog = trackerLog;

        [Command("Untrack")]
        [Description("Stops the logging of the user's activity.")]
        public async Task UnTrackAsync(SlashCommandContext ctx,
            [Parameter("User")] [Description("The user to stop logging.")]
            DiscordUser member)
        {
            await ctx.DeferResponseAsync();

            if (ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");

            var response = await this._mediator.Send(new Request { UserId = member.Id, GuildId = ctx.Guild.Id });


            await ctx.EditReplyAsync(message: $"Tracker removed from {member.Mention}");

            await this._trackerLog.SendTrackerMessageAsync(new TrackerMessage
            {
                GuildId = ctx.Guild.Id,
                TrackerId = response.TrackerChannelId,
                TrackerIdType = TrackerIdType.ChannelId,
                Color = GrimoireColor.Purple,
                Description = $"{ctx.User.Username} removed a tracker on {member.Mention}"
            });

            await this._guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = ctx.Guild.Id,
                GuildLogType = GuildLogType.Moderation,
                Color = GrimoireColor.Purple,
                Description = $"{ctx.User.Username} removed a tracker on {member.Mention}"
            });
        }
    }

    public sealed record Request : IRequest<Response>
    {
        public ulong UserId { get; init; }
        public GuildId GuildId { get; init; }
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
                .FirstOrDefaultAsync(cancellationToken);
            if (result is null)
                throw new AnticipatedException("Could not find a tracker for that user.");

            dbContext.Trackers.Remove(result);
            await dbContext.SaveChangesAsync(cancellationToken);

            return new Response { TrackerChannelId = result.LogChannelId };
        }
    }

    public sealed record Response
    {
        public ulong TrackerChannelId { get; init; }
    }
}
