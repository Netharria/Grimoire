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
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Moderation.Lock.Commands;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequirePermissions([DiscordPermission.ManageChannels], [DiscordPermission.ManageMessages])]
public sealed class LockChannel(SettingsModule settingsModule, GuildLog guildLog)
{
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;

    [Command("Lock")]
    [Description("Locks a channel for a specified amount of time.")]
    public async Task LockChannelAsync(
        CommandContext ctx,
        [Parameter("DurationType")] [Description("Select whether the duration will be in minutes hours or days.")]
        DurationType durationType,
        [MinMaxValue(0)] [Parameter("DurationAmount")] [Description("The amount of time the lock will last.")]
        int durationAmount,
        [ChannelTypes(DiscordChannelType.Text, DiscordChannelType.PublicThread, DiscordChannelType.PrivateThread,
            DiscordChannelType.Category, DiscordChannelType.GuildForum)]
        [Parameter("Channel")]
        [Description("The channel to lock. Current channel if not specified.")]
        DiscordChannel? channel = null,
        [MinMaxLength(maxLength: 1000)] [Parameter("Reason")] [Description("The reason for the lock.")]
        string? reason = null)
    {
        await ctx.DeferResponseAsync();
        channel ??= ctx.Channel;

        var guild = ctx.Guild!;

        if (channel.IsThread)
            await ThreadLockAsync(guild, ctx.GetModeratorId(), channel, reason, durationType, durationAmount);
        else if (channel.Type is DiscordChannelType.Text
                 or DiscordChannelType.Category
                 or DiscordChannelType.GuildForum)
            await ChannelLockAsync(guild, ctx.GetModeratorId(), channel, reason, durationType, durationAmount);
        else
        {
            await ctx.EditReplyAsync(message: "Channel not of valid type.");
            return;
        }

        await ctx.EditReplyAsync(
            message: $"{channel.Mention} has been locked for {durationAmount} {durationType}");

        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Purple,
            Description =
                $"{channel.Mention} has been locked for {durationAmount} {durationType} by {ctx.User.Mention}"
                + (string.IsNullOrWhiteSpace(reason) ? "" : $" for {reason}")
        });
    }

    private async Task ChannelLockAsync(DiscordGuild guild, ModeratorId moderatorId, DiscordChannel channel,
        string? reason,
        DurationType durationType, long durationAmount)
    {
        var previousSetting = guild.Channels[channel.Id].PermissionOverwrites
            .First(x => x.Id == guild.EveryoneRole.Id);
        var lockEndTime = durationType.GetDateTimeOffset(durationAmount);
        await this._settingsModule.AddLock(
            moderatorId,
            guild.GetGuildId(),
            channel.GetChannelId(),
            previousSetting.GetPreviouslyAllowedPermissions(),
            previousSetting.GetPreviouslyDeniedPermissions(),
            reason ?? string.Empty,
            lockEndTime
        );
        await channel.AddOverwriteAsync(guild.EveryoneRole,
            previousSetting.Allowed.RevokeLockPermissions(),
            previousSetting.Denied.SetLockPermissions());
    }

    private async Task ThreadLockAsync(DiscordGuild guild, ModeratorId moderatorId, DiscordChannel channel,
        string? reason,
        DurationType durationType, long durationAmount) =>
        await this._settingsModule.AddLock(
            moderatorId,
            guild.GetGuildId(),
            channel.GetChannelId(),
            new PreviouslyAllowedPermissions(),
            new PreviouslyDeniedPermissions(),
            reason ?? string.Empty,
            durationType.GetDateTimeOffset(durationAmount)
        );
}
