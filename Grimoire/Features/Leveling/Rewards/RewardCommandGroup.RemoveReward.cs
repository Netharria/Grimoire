// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using Grimoire.Features.Shared.Channels.GuildLog;

namespace Grimoire.Features.Leveling.Rewards;

public sealed partial class RewardCommandGroup
{
    [Command("Remove")]
    [Description("Removes a reward from the server.")]
    public async Task RemoveAsync(CommandContext ctx,
        [Parameter("Role")] [Description("The role to be removed as a reward.")]
        DiscordRole role)
    {
        if (ctx.Guild is null)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "This command can only be used in a server.");
            return;
        }

        await ctx.DeferResponseAsync();

        var guildSettings = await this._settingsModule.GetGuildSettings(ctx.Guild.Id);

        var guildReward = guildSettings.Rewards.FirstOrDefault(x => x.RoleId == role.Id);

        if (guildReward is not null)
        {
            guildSettings.Rewards.Remove(guildReward);
            await this._settingsModule.UpdateGuildSettings(guildSettings);
        }

        await ctx.EditReplyAsync(GrimoireColor.DarkPurple, $"Removed {role.Mention} reward");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.DarkPurple,
            Description = $"{ctx.User.Mention} removed {role.Mention} reward"
        });
    }
}
