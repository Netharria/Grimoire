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

        var rewardLines = await Task.WhenAll(rewards.Select(async x =>
        {
            var role = await guild.GetRoleOrDefaultAsync(x.RoleId);
            return $"Level:{x.RewardLevel} Role:{role?.Mention} {(x.RewardMessage.HasValue ? $"Reward Message: {x.RewardMessage.Value.Value}" : "")}";
        }));

        await ctx.ReplyAsync(GrimoireColor.DarkPurple,
            title: "Rewards",
            message: string.Join('\n', rewardLines));
    }
}
