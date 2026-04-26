// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
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
        [Parameter("Role")] [Description("The role to be added as a reward.")]
        DiscordRole role,
        [MinMaxValue(0, int.MaxValue)] [Parameter("Level")] [Description("The level the reward is awarded at.")]
        int level,
        [MinMaxLength(maxLength: 4096)]
        [Parameter("Message")]
        [Description("The message to send to users when they earn a reward. Discord Markdown applies.")]
        string? message = null)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        await ValidateBotHasPermission(guild, role)
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
                errors => OnFail(ctx, errors)
            );
    }

    private static Validation<DiscordRole> ValidateBotHasPermission(DiscordGuild guild, DiscordRole role)
        => guild.CurrentMember.Hierarchy < role.Position
            ? Validation<DiscordRole>.Fail(new Error("reward.role-id.bot-permissions",
                $"{guild.CurrentMember.DisplayName} will not be able to apply this " +
                $"reward role because the role has a higher rank than it does."))
            : Validation<DiscordRole>.Succeed(role);

    private static Task OnFail(CommandContext ctx, ImmutableArray<Error> errors)
    {
        return ctx.ReplyAsync(GrimoireColor.Red,
                $"Was not able to update the rewards for the server for the following errors: \n" +
                $"{string.Join('\n', errors.Distinct().Select(error => error.Message))}")
            .AsTask();
    }


    private async Task OnAddSuccess(CommandContext ctx, RewardAdded reward, DiscordRole role, DiscordGuild guild)
    {
        var responseMessage =
            $"Successfully updated the rewards to include {role.Mention} at level {reward.RewardLevel}.";

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
