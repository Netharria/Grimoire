// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using DSharpPlus.Commands.ContextChecks;
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

        var rewards = await this._settingsModule.GetLevelingRewardsAsync(guild.GetGuildId());

        await ctx.EditReplyAsync(GrimoireColor.DarkPurple,
            title: "Rewards",
            message: string.Join('\n', rewards
                .ToAsyncEnumerable()
                .SelectAwait(async x =>
                {
                    var role = await guild.GetRoleOrDefaultAsync(x.RoleId);
                    return
                        $"Level:{x.RewardLevel} Role:{role?.Mention} {(x.RewardMessage == null ? "" : $"Reward Message: {x.RewardMessage}")}";
                })));
    }
}
