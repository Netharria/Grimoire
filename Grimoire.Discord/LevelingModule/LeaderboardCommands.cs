// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Leveling.Queries.GetLeaderboard;

namespace Grimoire.Discord.LevelingModule;

[SlashRequireGuild]
[SlashRequireModuleEnabled(Module.Leveling)]
public class LeaderboardCommands : ApplicationCommandModule
{
    private readonly IMediator _mediator;

    public LeaderboardCommands(IMediator mediator)
    {
        this._mediator = mediator;
    }

    [SlashCommand("Leaderboard", "Posts the leaderboard for the server.")]
    public async Task LeaderboardAsync(InteractionContext ctx,
        [Choice("Top", 0)]
        [Choice("Me", 1)]
        [Choice("User", 2)]
        [Option("Option", "The leaderboard search type.")] long option,
        [Option("User", "User to find on the leaderboard.")] DiscordUser? user = null)
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

        var getUserCenteredLeaderboardQuery = new GetLeaderboardQuery
        {
            UserId = user?.Id,
            GuildId = ctx.Guild.Id,
        };

        var response = await this._mediator.Send(getUserCenteredLeaderboardQuery);

        await ctx.ReplyAsync(
            color: GrimoireColor.DarkPurple,
            title: "LeaderBoard",
            message: response.LeaderboardText,
            footer: $"Total Users {response.TotalUserCount}",
            ephemeral: !((DiscordMember)ctx.User).Permissions.HasPermission(Permissions.ManageMessages));
    }
}
