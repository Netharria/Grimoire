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
using Grimoire.Settings.Helpers;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Moderation.Lock.Commands;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequirePermissions([DiscordPermission.ManageChannels], [DiscordPermission.ManageMessages])]
public sealed class UnlockChannel(SettingsModule settingsModule, GuildLog guildLog)
{
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;

    [Command("Unlock")]
    [Description("Unlocks a channel.")]
    public async Task UnlockChannelAsync(
        CommandContext ctx,
        [Parameter("Channel")] [Description("The channel to unlock. Current channel if not specified.")]
        DiscordChannel? channel = null)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;
        channel ??= ctx.Channel;
        var moderatorId = ctx.GetModeratorId();

        var wasLocked = channel.IsThread
            ? await this.TryUnlockThreadAsync(guild, channel, moderatorId)
            : await this.TryUnlockChannelAsync(guild, channel, moderatorId);

        if (!wasLocked)
        {
            await ctx.ReplyAsync(message: $"{channel.Mention} is not locked.");
            return;
        }

        await ctx.ReplyAsync(message: $"{channel.Mention} has been unlocked");

        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Purple,
            Description = $"{ctx.User.Mention} unlocked {channel.Mention}"
        });
    }

    private async Task<bool> TryUnlockThreadAsync(DiscordGuild guild, DiscordChannel channel, ModeratorId moderatorId)
    {
        var response = await this._settingsModule.RemoveThreadLock(
            channel.GetChannelId(), guild.GetGuildId(), moderatorId);
        return response is SettingsWritten<ThreadLockEvent?>;
    }

    private async Task<bool> TryUnlockChannelAsync(DiscordGuild guild, DiscordChannel channel, ModeratorId moderatorId)
    {
        var response = await this._settingsModule.RemoveChannelLock(
            channel.GetChannelId(), guild.GetGuildId(), moderatorId);
        if (response is not SettingsWritten<ChannelLockEvent?> { InputValue: { } lockedChannel })
            return false;

        var permissions = guild.Channels[channel.Id].PermissionOverwrites
            .First(x => x.Id == guild.EveryoneRole.Id);
        await channel.AddOverwriteAsync(guild.EveryoneRole,
            permissions.Allowed.RevertLockPermissions(lockedChannel.PreviouslyAllowed.Permissions),
            permissions.Denied.RevertLockPermissions(lockedChannel.PreviouslyDenied.Permissions));
        return true;
    }
}
