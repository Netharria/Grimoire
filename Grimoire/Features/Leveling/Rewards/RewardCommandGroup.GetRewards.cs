// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using DSharpPlus.Commands.ContextChecks;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Leveling.Rewards;

public sealed partial class RewardCommandGroup
{
    [RequireGuild]
    [RequireModuleEnabled(Module.Leveling)]
    [RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
    [Command("View")]
    [Description("Displays all rewards on this server.")]
    public async Task ViewAsync(CommandContext ctx)
    {
        await ctx.DeferResponseAsync();
        var guild = ctx.Guild!;

        if (await this._settingsModule.GetLevelingRewardsAsync(guild.GetGuildId())
            is not Result<IReadOnlySet<RewardEntry>>.Success { Value: var rewards })
        {
            await ctx.SendErrorResponseAsync("Could not load the rewards. Please try again later.");
            return;
        }

        if (rewards.Count == 0)
        {
            await ctx.ReplyAsync(GrimoireColor.DarkPurple, title: "Rewards", message: "This server does not have any rewards.");
            return;
        }

        await ctx.ReplyPagedAsync(
            new DiscordEmbedBuilder().WithColor(GrimoireColor.DarkPurple).WithAuthor("Rewards"),
            DescribeRewards(rewards));
    }

    /// <summary>One line per reward, by level. The role is a mention, so a deleted role still shows its id.</summary>
    internal static IEnumerable<string> DescribeRewards(IEnumerable<RewardEntry> rewards)
        => rewards.OrderBy(reward => reward.RewardLevel).Select(reward =>
            $"Level:{reward.RewardLevel} Role:{RoleExtensions.Mention(reward.RoleId)}"
            + (reward.RewardMessage is { } message ? $" Reward Message: {message.Value}" : ""));
}
