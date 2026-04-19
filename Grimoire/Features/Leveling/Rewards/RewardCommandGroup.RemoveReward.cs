// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
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

        await
                Reward.Create(
                    role.GetRoleId(),
                    guild.GetGuildId(),
                    1,
                    null,
                    ctx.GetModeratorId(),
                    DateTimeOffset.UtcNow,
                    true)
            .BindAsync(async reward => (await this._settingsModule.SetRewardAsync(reward)).ToValidation())
            .Match(
                reward => OnAddSuccess(ctx, reward, role, guild),
                errors => OnFail(ctx, errors)
            );
    }

    private async Task OnRemoveSuccess(CommandContext ctx, Reward reward, DiscordRole role, DiscordGuild guild)
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
