// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.Features.Shared.Queries;

namespace Grimoire.Features.Leveling.UserCommands;

public sealed class GetLeaderboard
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Leveling)]
    public sealed class Command(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        [SlashCommand("Leaderboard", "Posts the leaderboard for the server.")]
        public async Task LeaderboardAsync(InteractionContext ctx,
            [Choice("Top", 0)] [Choice("Me", 1)] [Choice("User", 2)] [Option("Option", "The leaderboard search type.")]
            long option,
            [Option("User", "User to find on the leaderboard.")]
            DiscordUser? user = null)
        {
            switch (option)
            {
                case 0:
                    user = null;
                    break;
                case 1:
                    user = ctx.User;
                    break;
                case 2:
                    if (user is null)
                        throw new AnticipatedException("Must provide a user for this option.");
                    break;
            }

            var userCommandChannel =
                await this._mediator.Send(new GetUserCommandChannel.Query { GuildId = ctx.Guild.Id });

            await ctx.DeferAsync(!ctx.Member.Permissions.HasPermission(DiscordPermissions.ManageMessages)
                                 && userCommandChannel?.UserCommandChannelId != ctx.Channel.Id);

            var getUserCenteredLeaderboardQuery = new Request { UserId = user?.Id, GuildId = ctx.Guild.Id };

            var response = await this._mediator.Send(getUserCenteredLeaderboardQuery);
            await ctx.EditReplyAsync(
                GrimoireColor.DarkPurple,
                title: "LeaderBoard",
                message: response.LeaderboardText,
                footer: $"Total Users {response.TotalUserCount}");
        }
    }

    public sealed record Request : IRequest<Response>
    {
        public required ulong GuildId { get; init; }
        public ulong? UserId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var query = dbContext.Members
                .AsNoTracking()
                .Where(member => member.GuildId == request.GuildId)
                .Select(member => new { member.UserId, Xp = member.XpHistory.Sum(xp => xp.Xp) })
                .OrderByDescending(x => x.Xp);

            if (request.UserId is null)
            {
                var rankedMembers = await query
                    .Take(15)
                    .ToListAsync(cancellationToken);

                var totalMemberCount = await dbContext.Members
                    .AsNoTracking()
                    .Where(x => x.GuildId == request.GuildId)
                    .CountAsync(cancellationToken);

                var leaderboardText = new StringBuilder();
                for (var i = 0; i < 15 && i < totalMemberCount; i++)
                    leaderboardText.Append(
                        $"**{i + 1}** {UserExtensions.Mention(rankedMembers[i].UserId)} **XP:** {rankedMembers[i].Xp}\n");
                return new Response { LeaderboardText = leaderboardText.ToString(), TotalUserCount = totalMemberCount };
            }
            else
            {
                var rankedMembers = await query.ToListAsync(cancellationToken);

                var totalMemberCount = rankedMembers.Count;

                var memberPosition = rankedMembers.FindIndex(x => x.UserId == request.UserId);

                if (memberPosition == -1)
                    throw new AnticipatedException("Could not find user on leaderboard.");

                var startIndex = Math.Max(0, memberPosition - 5);
                startIndex = Math.Min(startIndex, totalMemberCount - 15);
                startIndex = Math.Max(0, startIndex);

                var leaderboardText = new StringBuilder();
                for (var i = 0; i < 15 && startIndex < totalMemberCount; i++, startIndex++)
                    leaderboardText.Append(
                        $"**{startIndex + 1}** {UserExtensions.Mention(rankedMembers[startIndex].UserId)} **XP:** {rankedMembers[startIndex].Xp}\n");

                return new Response { LeaderboardText = leaderboardText.ToString(), TotalUserCount = totalMemberCount };
            }
        }
    }

    public sealed record Response : BaseResponse
    {
        public required string LeaderboardText { get; init; }
        public required int TotalUserCount { get; init; }
    }
}
