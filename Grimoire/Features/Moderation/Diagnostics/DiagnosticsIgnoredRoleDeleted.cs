// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Moderation.Diagnostics;

/// <summary>
///     Takes a role off the diagnostics ignore list when Discord deletes it. A deleted role can't be picked in
///     <c>/Diagnostics Ignore Remove</c>, so it would otherwise stay on the list for good (KI-62).
/// </summary>
public sealed class DiagnosticsIgnoredRoleDeleted(SettingsModule settingsModule, GuildLog guildLog)
    : IEventHandler<GuildRoleDeletedEventArgs>
{
    public async Task HandleEventAsync(DiscordClient sender, GuildRoleDeletedEventArgs args)
    {
        var guildId = args.Guild.GetGuildId();
        if (await settingsModule.RemoveDiagnosticsIgnoredRoleAsync(guildId, args.Role.GetRoleId())
            is not Result<Unit>.Success)
            return;

        await guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guildId,
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Purple,
            Description =
                $"The role **{args.Role.Name}** (`{args.Role.Id}`) was deleted, so it is no longer ignored by the diagnostics."
        });
    }
}
