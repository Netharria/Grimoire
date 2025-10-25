// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Services;

namespace Grimoire.Features.Moderation.Mute;

public sealed class UserJoinedWhileMuted(SettingsModule settingsModule) : IEventHandler<GuildMemberAddedEventArgs>
{
    private readonly SettingsModule _settingsModule = settingsModule;

    public async Task HandleEventAsync(DiscordClient sender, GuildMemberAddedEventArgs args)
    {
        if (!await this._settingsModule.IsMemberMuted(args.Member.Id, args.Guild.Id))
            return;
        var muteRole = await this._settingsModule.GetMuteRole(args.Member.Id);
        if (muteRole is null) return;
        var role = args.Guild.Roles.GetValueOrDefault(muteRole.Value);
        if (role is null) return;
        await args.Member.GrantRoleAsync(role, "Rejoined while muted");
    }
}
