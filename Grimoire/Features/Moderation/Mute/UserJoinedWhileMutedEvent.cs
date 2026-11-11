// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Moderation.Mute;

public sealed class UserJoinedWhileMuted(SettingsModule settingsModule) : IEventHandler<GuildMemberAddedEventArgs>
{
    public async Task HandleEventAsync(DiscordClient sender, GuildMemberAddedEventArgs args)
    {
        var guildId = args.Guild.GetGuildId();
        if (await settingsModule.GetActiveMute(args.Member.GetUserId(), guildId)
            is not Result<MuteAdded>.Success { Value: var mute } || mute.EndTime <= DateTimeOffset.UtcNow)
            return;
        if (!await settingsModule.IsModuleEnabled(Module.Moderation, guildId).GetOrElse(() => false))
            return;

        var configuredRoleId = await settingsModule.GetConfiguredMuteRole(guildId).GetOrElse(() => null);
        if (MuteRoleResolver.Resolve(mute.MuteRoleId, configuredRoleId, id => args.Guild.Roles.ContainsKey(id.Value))
            is not { } roleId)
            return;

        await args.Member.GrantRoleAsync(await args.Guild.GetRoleAsync(roleId.Value), "Rejoined while muted");
    }
}
