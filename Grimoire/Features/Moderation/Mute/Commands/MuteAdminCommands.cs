// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using JetBrains.Annotations;

namespace Grimoire.Features.Moderation.Mute.Commands;

[UsedImplicitly]
[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
[RequirePermissions([DiscordPermission.ManageRoles], [])]
[Command("Mutes")]
[Description("Manages the mute role settings.")]
public sealed partial class MuteAdminCommands(SettingsModule settingsModule, GuildLog guildLog)
{
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;

    private static async IAsyncEnumerable<OverwriteChannelResult> SetMuteRolePermissionsAsync(DiscordGuild guild,
        DiscordRole role)
    {
        foreach (var (_, channel) in guild.Channels)
            switch (channel.Type)
            {
                case DiscordChannelType.Text or DiscordChannelType.Category or DiscordChannelType.GuildForum:
                    yield return await OverwriteChannelAsync(channel, role);
                    break;
                case DiscordChannelType.Voice:
                    yield return await OverwriteVoiceChannelAsync(channel, role);
                    break;
            }
    }

    private static async Task<OverwriteChannelResult> OverwriteChannelAsync(DiscordChannel channel, DiscordRole role)
    {
        var permissions = channel.PermissionOverwrites.FirstOrDefault(x => x.Id == role.Id);
        return await DiscordRetryPolicy.RetryDiscordCall(async _ =>
        {
            if (permissions is not null)
                await channel.AddOverwriteAsync(role,
                    permissions.Allowed.RevokeLockPermissions(),
                    permissions.Denied.SetLockPermissions());
            else
                await channel.AddOverwriteAsync(role,
                    deny: PermissionValues.LockPermissions);
            return new OverwriteChannelResult { WasSuccessful = true, Channel = channel };
        }, new OverwriteChannelResult { WasSuccessful = false, Channel = channel });
    }

    private static async Task<OverwriteChannelResult> OverwriteVoiceChannelAsync(DiscordChannel channel,
        DiscordRole role)
    {
        var permissions = channel.PermissionOverwrites.FirstOrDefault(x => x.Id == role.Id);

        return await DiscordRetryPolicy.RetryDiscordCall(async _ =>
        {
            if (permissions is not null)
                await channel.AddOverwriteAsync(role,
                    permissions.Allowed.RevokeVoiceLockPermissions(),
                    permissions.Denied.SetVoiceLockPermissions());
            else
                await channel.AddOverwriteAsync(role,
                    deny: PermissionValues.VoiceLockPermissions);
            return new OverwriteChannelResult { WasSuccessful = true, Channel = channel };
        }, new OverwriteChannelResult { WasSuccessful = false, Channel = channel });
    }
}
