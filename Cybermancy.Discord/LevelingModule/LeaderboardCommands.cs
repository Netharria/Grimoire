// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Attributes;
using Cybermancy.Core.Enums;
using Cybermancy.Core.Features.Leveling.Queries.GetLeaderboard;
using Cybermancy.Enums;
using Cybermancy.Extensions;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using MediatR;

namespace Cybermancy.LevelingModule
{
    [SlashCommandGroup("Leaderboard", "Posts the leaderboard for the server.")]
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Leveling)]
    public class LeaderboardCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public LeaderboardCommands(IMediator mediator)
        {
            this._mediator = mediator;
        }

        [SlashCommand("Me", "Find out where you are on the Leaderboard.")]
        public Task MeAsync(InteractionContext ctx) =>
            this.UserAsync(ctx, ctx.User);

        [SlashCommand("User", "Find out where someone are on the Leaderboard.")]
        public async Task UserAsync(InteractionContext ctx,
            [Option("User", "User to find on the leaderboard. Leave empty for top of the rankings")] DiscordUser? user = null)
        {
            var getUserCenteredLeaderboardQuery = new GetLeaderboardQuery
            {
                UserId = user?.Id,
                GuildId = ctx.Guild.Id,
            };

            var response = await this._mediator.Send(getUserCenteredLeaderboardQuery);
            if (response.Success)
                await ctx.ReplyAsync(
                    color: CybermancyColor.Gold,
                    title: "LeaderBoard",
                    message: response.LeaderboardText,
                    footer: $"Total Users {response.TotalUserCount}",
                    ephemeral: !((DiscordMember)ctx.User).Permissions.HasPermission(Permissions.ManageMessages));
            else
                await ctx.ReplyAsync(
                    color: CybermancyColor.Orange,
                    message: response.Message,
                    ephemeral: true);
        }
    }
}
