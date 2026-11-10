// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using JetBrains.Annotations;

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

        await this._settingsModule.GetLevelingRewardsAsync(guild.GetGuildId())
            .Bind(rewards => rewards.Any(reward => reward.RoleId == role.GetRoleId())
                ? Result<Unit>.Ok(Unit.Value)
                : new Result<Unit>.NotFound(new Error("reward.not-found", $"Did not find a saved reward for {role.Mention}.")))
            .BindAsync(_ => RewardRemoved.Create(role.GetRoleId(), guild.GetGuildId(), ctx.GetModeratorId(), DateTimeOffset.UtcNow)
                .ToResult()
                .BindAsync(reward => this._settingsModule.SetRewardAsync(reward)))
            .Match(
                _ => OnRemoveSuccess(ctx, role, guild),
                error => OnFail(ctx, error));
    }

    private async Task OnRemoveSuccess(CommandContext ctx, DiscordRole role, DiscordGuild guild)
    {
        var responseMessage = $"{ctx.User.Mention} removed {role.Mention} reward";

        await ctx.ReplyAsync(GrimoireColor.DarkPurple, responseMessage);
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.DarkPurple,
            Description = responseMessage
        });
    }
}
