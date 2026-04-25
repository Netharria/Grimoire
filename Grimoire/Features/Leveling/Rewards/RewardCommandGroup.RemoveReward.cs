// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using JetBrains.Annotations;

namespace Grimoire.Features.Leveling.Rewards;

public sealed partial class RewardCommandGroup
{
    [UsedImplicitly]
    [Command("Remove")]
    [Description("Removes a reward from the server.")]
    public async Task RemoveAsync(CommandContext ctx,
        [Parameter("Role")] [Description("The role to be removed as a reward.")]
        DiscordRole role)
    {
        var guild = ctx.Guild!;

        await ctx.DeferResponseAsync();

        if (RewardRemoved.Create(role.GetRoleId(), guild.GetGuildId(), ctx.GetModeratorId(), DateTimeOffset.UtcNow)
            is not Validation<RewardRemoved>.Valid(var removeReward))
        {
            await ctx.EditResponseAsync("Failed to create reward removal event.");
            return;
        }

        var result = await this._settingsModule.SetRewardAsync(removeReward);

        await result.Match(
            _ => OnRemoveSuccess(ctx, role, guild),
            errors => OnFail(ctx, errors));
    }

    private async Task OnRemoveSuccess(CommandContext ctx, DiscordRole role, DiscordGuild guild)
    {
        var responseMessage =
            $"{ctx.User.Mention} removed {role.Mention} reward";

        await ctx.ReplyAsync(GrimoireColor.DarkPurple, responseMessage);
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Leveling,
            Color = GrimoireColor.DarkPurple,
            Description = responseMessage
        });
    }
}
