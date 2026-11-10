// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Values;
using Grimoire.Settings.Enums;
using JetBrains.Annotations;

namespace Grimoire.Features.Leveling.Rewards;

[UsedImplicitly]
public sealed partial class RewardCommandGroup
{
    [UsedImplicitly]
    [RequireGuild]
    [RequireModuleEnabled(Module.Leveling)]
    [RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
    [Command("Add")]
    [Description("Adds or updates rewards for the server.")]
    public async Task AddAsync(CommandContext ctx,
        [Parameter("role")] [Description("The role to be added as a reward.")]
        DiscordRole role,
        [MinMaxValue(0, int.MaxValue)] [Parameter("level")] [Description("The level the reward is awarded at.")]
        int level,
        [MinMaxLength(maxLength: 4096)]
        [Parameter("message")]
        [Description("The message to send to users when they earn a reward. Discord Markdown applies.")]
        string? message = null)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.RequiredGuild;

        await ValidateBotCanGrant(guild, role)
            .Bind(_ => RewardMessage.CreateIfNotNull(message))
            .Bind(validatedMessage =>
                RewardAdded.Create(
                    role.GetRoleId(),
                    guild.GetGuildId(),
                    level,
                    validatedMessage,
                    ctx.GetModeratorId(),
                    DateTimeOffset.UtcNow)
            )
            .ToResult()
            .BindAsync(async reward => await this._settingsModule.SetRewardAsync(reward))
            .Match(
                reward => OnAddSuccess(ctx, reward, role, guild),
                error => OnFail(ctx, error)
            );
    }

    private static Validation<DiscordRole> ValidateBotCanGrant(DiscordGuild guild, DiscordRole role)
        => role switch
        {
            { IsManaged: true } => Validation<DiscordRole>.Fail(new Error("reward.role-id.managed",
                "That role is managed by an integration, such as a bot or Server Booster, so it can't be given out.")),
            // Discord needs the bot's top role strictly above the role it grants.
            _ when role.Position >= guild.CurrentMember.Hierarchy => Validation<DiscordRole>.Fail(new Error(
                "reward.role-id.bot-permissions",
                $"{guild.CurrentMember.DisplayName} will not be able to apply this " +
                $"reward role because the role has the same or a higher rank than it does.")),
            _ => Validation<DiscordRole>.Succeed(role)
        };

    private static Task OnFail(CommandContext ctx, Error error)
        => ctx.ReplyAsync(GrimoireColor.Red,
                $"Was not able to update the rewards for the server:\n{error.Message}")
            .AsTask();


    private async Task OnAddSuccess(CommandContext ctx, RewardAdded reward, DiscordRole role, DiscordGuild guild)
    {
        var responseMessage =
            $"Successfully updated the rewards to include {role.Mention} at level {reward.RewardLevel}.";

        await ctx.ReplyAsync(GrimoireColor.DarkPurple, responseMessage);
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.DarkPurple,
            Description = $"{ctx.User.Mention} updated the rewards to include {role.Mention} at level {reward.RewardLevel}."
        });
    }
}
