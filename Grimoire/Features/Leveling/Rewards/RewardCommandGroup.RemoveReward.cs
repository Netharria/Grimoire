// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Leveling.Rewards;

public sealed partial class RewardCommandGroup
{
    [Command("Remove")]
    [Description("Removes a reward from the server.")]
    public async Task RemoveAsync(CommandContext ctx,
        [Parameter("Role")] [Description("The role to be removed as a reward.")]
        DiscordRole role)
    {
        var guild = ctx.Guild!;

        await ctx.DeferResponseAsync();

        await this._settingsModule.RemoveRewardAsync(role.GetRoleId(), guild.GetGuildId());

        await ctx.EditReplyAsync(GrimoireColor.DarkPurple, $"Removed {role.Mention} reward");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.DarkPurple,
            Description = $"{ctx.User.Mention} removed {role.Mention} reward"
        });
    }
}
