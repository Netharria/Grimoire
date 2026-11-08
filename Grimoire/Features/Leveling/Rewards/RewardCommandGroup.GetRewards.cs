// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using System.Text;
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

        await ctx.ReplyAsync(GrimoireColor.DarkPurple, title: "Rewards", message: DescribeRewards(rewards));
    }

    private const int MaxListedMessageLength = 200;
    private const int MaxDescriptionLength = 4096;

    /// <summary>
    ///     One line per reward, by level. A reward message can be 4,096 characters by itself, so it's cut short here,
    ///     and lines stop before the embed's limit. The role is a mention, so a deleted role still shows its id.
    /// </summary>
    internal static string DescribeRewards(IEnumerable<RewardEntry> rewards)
    {
        var ordered = rewards.OrderBy(reward => reward.RewardLevel).ToArray();
        var description = new StringBuilder();
        var shown = 0;
        foreach (var reward in ordered)
        {
            var line = $"Level:{reward.RewardLevel} Role:{RoleExtensions.Mention(reward.RoleId)}"
                       + (reward.RewardMessage is { } message ? $" Reward Message: {Shorten(message.Value)}" : "") + "\n";
            if (description.Length + line.Length > MaxDescriptionLength - 40)
                break;
            description.Append(line);
            shown++;
        }

        if (shown < ordered.Length)
            description.Append("…and ").Append(ordered.Length - shown).Append(" more rewards.");
        return description.ToString().TrimEnd('\n');
    }

    private static string Shorten(string message)
        => message.Length <= MaxListedMessageLength ? message : string.Concat(message.AsSpan(0, MaxListedMessageLength - 1), "…");
}
