// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Logging.Trackers.Commands;

public sealed class AddTracker
{
    [RequireGuild]
    [RequireModuleEnabled(Module.MessageLog)]
    [RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
    internal sealed class Command(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        [SlashCommand("Track", "Creates a log of a user's activity into the specified channel.")]
        public async Task TrackAsync(InteractionContext ctx,
            [Option("User", "User to log.")] DiscordUser user,
            [Option("DurationType", "Select whether the duration will be in minutes hours or days")]
            DurationType durationType,
            [Minimum(0)] [Option("DurationAmount", "Select the amount of time the logging will last.")]
            long durationAmount,
            [Option("Channel", "Select the channel to log to. Current channel if left blank.")]
            DiscordChannel? discordChannel = null)
        {
            await ctx.DeferAsync();
            if (user.Id == ctx.Client.CurrentUser.Id)
            {
                await ctx.EditReplyAsync(message: "Why would I track myself?");
                return;
            }

            if (ctx.Guild.Members.TryGetValue(user.Id, out var member))
                if (member.Permissions.HasPermission(DiscordPermission.ManageGuild))
                {
                    await ctx.EditReplyAsync(message: "<_<\n>_>\nI can't track a mod.\n Try someone else");
                    return;
                }


            discordChannel ??= ctx.Channel;

            if (!ctx.Guild.Channels.ContainsKey(discordChannel.Id))
            {
                await ctx.EditReplyAsync(
                    message: "<_<\n>_>\nThat channel is not on this server.\n Try a different one.");
                return;
            }

            var permissions = discordChannel.PermissionsFor(ctx.Guild.CurrentMember);
            if (!permissions.HasPermission(DiscordPermission.SendMessages))
                throw new AnticipatedException(
                    $"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");

            var response = await this._mediator.Send(
                new Request
                {
                    UserId = user.Id,
                    GuildId = ctx.Guild.Id,
                    Duration = durationType.GetTimeSpan(durationAmount),
                    ChannelId = discordChannel.Id,
                    ModeratorId = ctx.Member.Id
                });

            await ctx.EditReplyAsync(
                message:
                $"Tracker placed on {user.Mention} in {discordChannel.Mention} for {durationAmount} {durationType.GetName()}");


            await ctx.Client.SendMessageToLoggingChannel(response.ModerationLogId,
                embed => embed
                    .WithDescription(
                        $"{ctx.Member.GetUsernameWithDiscriminator()} placed a tracker on {user.Mention} in {discordChannel.Mention} for {durationAmount} {durationType.GetName()}")
                    .WithColor(GrimoireColor.Purple));
        }
    }


    public sealed record Request : IRequest<Response>
    {
        public ulong UserId { get; init; }
        public ulong GuildId { get; init; }
        public TimeSpan Duration { get; init; }
        public ulong ChannelId { get; init; }
        public ulong ModeratorId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Request command, CancellationToken cancellationToken)
        {
            var trackerEndTime = DateTimeOffset.UtcNow + command.Duration;
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbContext.Guilds
                .Where(x => x.Id == command.GuildId)
                .Select(x =>
                    new
                    {
                        Tracker = x.Trackers.FirstOrDefault(y => y.UserId == command.UserId),
                        x.ModChannelLog,
                        MemberExist = x.Members.Any(y => y.UserId == command.UserId)
                    })
                .FirstOrDefaultAsync(cancellationToken);
            if (result?.Tracker is null)
            {
                var local = dbContext.Trackers.Local
                    .FirstOrDefault(x => x.UserId == command.UserId
                                         && x.GuildId == command.GuildId);
                if (local is not null)
                    dbContext.Entry(local).State = EntityState.Detached;
                if (result?.MemberExist is null || !result.MemberExist)
                {
                    if (!await dbContext.Users.WhereIdIs(command.UserId).AnyAsync(cancellationToken))
                        await dbContext.Users.AddAsync(new User { Id = command.UserId },
                            cancellationToken);
                    await dbContext.Members.AddAsync(new Member
                    {
                        UserId = command.UserId,
                        GuildId = command.GuildId,
                        XpHistory =
                        [
                            new XpHistory
                            {
                                UserId = command.UserId,
                                GuildId = command.GuildId,
                                Xp = 0,
                                Type = XpHistoryType.Created,
                                TimeOut = DateTime.UtcNow
                            }
                        ]
                    }, cancellationToken);
                }

                await dbContext.Trackers.AddAsync(
                    new Tracker
                    {
                        UserId = command.UserId,
                        GuildId = command.GuildId,
                        EndTime = trackerEndTime,
                        LogChannelId = command.ChannelId,
                        ModeratorId = command.ModeratorId
                    }, cancellationToken);
            }
            else
            {
                result.Tracker.LogChannelId = command.ChannelId;
                result.Tracker.EndTime = trackerEndTime;
                result.Tracker.ModeratorId = command.ModeratorId;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return new Response { ModerationLogId = result?.ModChannelLog };
        }
    }

    public sealed record Response : BaseResponse
    {
        public ulong? ModerationLogId { get; init; }
    }
}
