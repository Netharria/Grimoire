// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace Grimoire.Features.Leveling.Rewards;

public sealed partial class RewardCommandGroup
{
    [Command("View")]
    [Description("Displays all rewards on this server.")]
    public async Task ViewAsync(CommandContext ctx)
    {
        await ctx.DeferResponseAsync();
        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");
        var response = await this._mediator.CreateStream(new GetRewards.Request { GuildId = ctx.Guild.Id })
            .SelectAwait(async x =>
            {
                var role = await ctx.Guild.GetRoleOrDefaultAsync(x.RoleId);
                return
                    $"Level:{x.RewardLevel} Role:{role?.Mention} {(x.RewardMessage == null ? "" : $"Reward Message: {x.RewardMessage}")}";
            })
            .ToArrayAsync();
        await ctx.EditReplyAsync(GrimoireColor.DarkPurple,
            title: "Rewards",
            message: string.Join('\n', response));
    }
}
