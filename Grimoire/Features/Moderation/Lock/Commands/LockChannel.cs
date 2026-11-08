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
[RequirePermissions([DiscordPermission.ManageChannels, DiscordPermission.ManageRoles], [DiscordPermission.ManageMessages])]
public sealed class LockChannel(SettingsModule settingsModule, GuildLog guildLog)
{
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;

    [Command("Lock")]
    [Description("Locks a channel for a specified amount of time.")]
    public async Task LockChannelAsync(
        CommandContext ctx,
        [Parameter("duration_type")] [Description("Select whether the duration will be in minutes hours or days.")]
        DurationType durationType,
        // 100 years in days. Far more overflows the end time, after nothing has been changed, with a generic error.
        [MinMaxValue(1, 36_500)] [Parameter("duration_amount")] [Description("The amount of time the lock will last.")]
        int durationAmount,
        [ChannelTypes(DiscordChannelType.Text, DiscordChannelType.PublicThread, DiscordChannelType.PrivateThread,
            DiscordChannelType.Category, DiscordChannelType.GuildForum)]
        [Parameter("channel")]
        [Description("The channel to lock. Current channel if not specified.")]
        DiscordChannel? channel = null,
        [MinMaxLength(maxLength: 1000)] [Parameter("reason")] [Description("The reason for the lock.")]
        string? reason = null)
    {
        await ctx.DeferResponseAsync();
        channel ??= ctx.Channel;

        var guild = ctx.Guild!;

        Error? failure;
        if (channel.IsThread)
            failure = (await ThreadLockAsync(guild, ctx.GetModeratorId(), channel, reason, durationType, durationAmount))
                .Match<Error?>(_ => null, error => error);
        else if (channel.Type is DiscordChannelType.Text
                 or DiscordChannelType.Category
                 or DiscordChannelType.GuildForum)
            failure = (await ChannelLockAsync(guild, ctx.GetModeratorId(), channel, reason, durationType, durationAmount))
                .Match<Error?>(_ => null, error => error);
        else
        {
            await ctx.ReplyAsync(message: "Channel not of valid type.");
            return;
        }

        if (failure is not null)
        {
            await ctx.SendErrorResponseAsync(failure.Message);
            return;
        }

        await ctx.ReplyAsync(
            message: $"{channel.Mention} has been locked for {durationAmount} {durationType}");

        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Purple,
            Description =
                $"{channel.Mention} has been locked for {durationAmount} {durationType} by {ctx.User.Mention}{ModerationDirectMessage.ForReason(reason)}"
        });
    }

    private Task<Result<ChannelLocked>> ChannelLockAsync(DiscordGuild guild, ModeratorId moderatorId,
        DiscordChannel channel,
        string? reason,
        DurationType durationType, long durationAmount)
    {
        // The @everyone overwrite should always exist, but a channel without one is treated as having no overrides.
        var previousSetting = channel.PermissionOverwrites
            .FirstOrDefault(x => x.Id == guild.EveryoneRole.Id);
        var previousAllowed = previousSetting?.Allowed ?? DiscordPermissions.None;
        var previousDenied = previousSetting?.Denied ?? DiscordPermissions.None;
        return ModerationReason.CreateIfNotNull(reason)
            .Bind(validatedReason =>
                ChannelLocked.Create(
                    moderatorId,
                    validatedReason,
                    channel.GetChannelId(),
                    guild.GetGuildId(),
                    DateTimeOffset.UtcNow,
                    new PreviouslyAllowedPermissions(long.Parse(previousAllowed.GetLockPermissions().ToString())),
                    new PreviouslyDeniedPermissions(long.Parse(previousDenied.GetLockPermissions().ToString())),
                    durationType.GetDateTimeOffset(durationAmount)))
            .ToResult()
            .TapAsync(_ =>
                channel.AddOverwriteAsync(guild.EveryoneRole,
                    previousAllowed.RevokeLockPermissions(),
                    previousDenied.SetLockPermissions()))
            .BindAsync(channelLock =>
                this._settingsModule.ApplyChannelLockAction(channelLock));
    }

    private Task<Result<ThreadLocked>> ThreadLockAsync(DiscordGuild guild, ModeratorId moderatorId,
        DiscordChannel channel,
        string? reason,
        DurationType durationType, long durationAmount) =>
        // A thread lock is enforced by deleting every new message, which takes Manage Messages in the thread.
        (channel.PermissionsFor(guild.CurrentMember).HasPermission(DiscordPermission.ManageMessages)
            ? ModerationReason.CreateIfNotNull(reason)
            : Validation<ModerationReason?>.Fail(new Error("thread-lock.bot-permissions",
                $"{guild.CurrentMember.Mention} needs **Manage Messages** in {channel.Mention} to lock it.")))
            .Bind(validatedReason =>
                ThreadLocked.Create(
                    moderatorId,
                    validatedReason,
                    channel.GetChannelId(),
                    guild.GetGuildId(),
                    DateTimeOffset.UtcNow,
                    durationType.GetDateTimeOffset(durationAmount)))
            .ToResult()
            .BindAsync(channelLock =>
                this._settingsModule.ApplyThreadLockAction(channelLock));
}
