// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using Grimoire.Core.Features.Leveling.Queries.GetLeaderboard;
using Grimoire.Core.Features.Leveling.Queries.GetLevel;

namespace Grimoire.Discord.TextCommands;

[RequireGuild]
[RequireModuleEnabled(Module.Leveling)]
public class LevelTextCommands : BaseCommandModule
{
    private readonly IMediator _mediator;
    const ulong GuildId = 539925898128785460;
    const ulong ChannelId = 613131646929207355;
    public LevelTextCommands(IMediator mediator)
    {
        this._mediator = mediator;
    }

    [Command("level")]
    public async Task LevelCommandAsync(CommandContext ctx, DiscordMember? member = null)
    {
        if (ctx.Guild.Id != GuildId)
            return;
        if (ctx.Member is null)
            return;
        if (ctx.Channel.Id != ChannelId
            && !ctx.Member.Permissions.HasPermission(Permissions.ManageMessages))
            return;

        member ??= ctx.Member;
        if (member is null)
        {
            await ctx.RespondAsync(new DiscordEmbedBuilder()
            .WithColor(GrimoireColor.Red)
            .WithDescription("Could not find that user on this server.")
            .Build());
            return;
        }
        var response = await this._mediator.Send(new GetLevelQuery{ UserId = member.Id, GuildId = member.Guild.Id});
        DiscordRole? roleReward = null;
        if (response.NextRoleRewardId is not null)
            roleReward = ctx.Guild.GetRole(response.NextRoleRewardId.Value);

        var embed = new DiscordEmbedBuilder()
            .WithColor(member.Color)
            .WithTitle($"Level and EXP for {member.DisplayName}")
            .AddField("XP", $"{response.UsersXp}", inline: true)
            .AddField("Level", $"{response.UsersLevel}", inline: true)
            .AddField("Progress", $"{response.LevelProgress}/{response.XpForNextLevel}", inline: true)
            .AddField("Next Reward", roleReward is null ? "None" : $"{roleReward.Mention}\n at level {response.NextRewardLevel}", inline: true)
            .WithThumbnail(member.GetGuildAvatarUrl(ImageFormat.Auto) ?? member.DefaultAvatarUrl)
            .WithFooter($"{ctx.Guild.Name}", ctx.Guild.IconUrl)
            .Build();
        await ctx.RespondAsync(embed);
    }

    [Command("leaderboard")]
    public async Task LeaderboardAsync(CommandContext ctx, string user = "all")
    {
        if (ctx.Guild.Id != GuildId)
            return;
        if (ctx.Member is null)
            return;
        if (ctx.Channel.Id != ChannelId
            && !ctx.Member.Permissions.HasPermission(Permissions.ManageMessages))
            return;
        DiscordMember? member = null;
        switch (user)
        {
            case "all":
                member = null;
                break;
            case "me":
                member = ctx.Member;
                break;
            default:
                var matchId = Regex.Match(user, @"(\d{17,21})", RegexOptions.None, TimeSpan.FromSeconds(1));
                if (matchId.Success)
                    if (ulong.TryParse(matchId.Value, out var userId))
                        ctx.Guild.Members.TryGetValue(userId, out member);
                break;
        }

        if (!user.Equals("all", StringComparison.OrdinalIgnoreCase)
            && member is null)
        {
            await ctx.RespondAsync(new DiscordEmbedBuilder()
            .WithColor(GrimoireColor.Red)
            .WithDescription("Could not find that user on this server.")
            .Build());
            return;
        }

        var getUserCenteredLeaderboardQuery = new GetLeaderboardQuery
        {
            UserId = member?.Id,
            GuildId = ctx.Guild.Id,
        };

        var response = await this._mediator.Send(getUserCenteredLeaderboardQuery);
        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Leaderboard")
            .WithDescription(response.LeaderboardText)
            .WithFooter($"Total Users {response.TotalUserCount}")
            .WithColor(GrimoireColor.DarkPurple)
            .WithTimestamp(DateTime.UtcNow)
            .Build();
        await ctx.RespondAsync(embed);
    }
}
