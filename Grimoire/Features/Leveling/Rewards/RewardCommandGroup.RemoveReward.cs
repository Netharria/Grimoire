// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using JetBrains.Annotations;
using LanguageExt;

namespace Grimoire.Features.Leveling.Rewards;

public sealed partial class RewardCommandGroup
{
    [UsedImplicitly]
    [RequireGuild]
    [RequireModuleEnabled(Module.Leveling)]
    [RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
    [Command("Remove")]
    [Description("Removes a reward from the server.")]
    public async Task RemoveAsync(CommandContext ctx,
        [Parameter("role")] [Description("The role to be removed as a reward.")]
        DiscordRole role)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.RequiredGuild;

        await this._settingsModule.RemoveRewardsAsync(guild.GetGuildId(), ctx.GetModeratorId(), [role.GetRoleId()])
            .Match(
                write => write.Match(
                    saved: _ => OnRemoveSuccess(ctx, role, guild),
                    // Decided against the version the write replaced (spec 2.5), so there's no separate check first.
                    unchanged: _ => OnFail(ctx, new NotFound($"Did not find a saved reward for {role.Mention}."))),
                error => OnFail(ctx, error));
    }

    private async Task OnRemoveSuccess(CommandContext ctx, DiscordRole role, DiscordGuild guild)
    {
        var responseMessage = $"{ctx.User.Mention} removed {role.Mention} reward";

        await ctx.ReplyAsync(GrimoireColor.DarkPurple, responseMessage);
        await this._guildLog.SendLogMessageAsync(new GuildLogTextMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.DarkPurple,
            Description = responseMessage
        });
    }
}
