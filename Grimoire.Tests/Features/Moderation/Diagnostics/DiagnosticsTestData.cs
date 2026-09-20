// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities;
using Grimoire.Features.Moderation.Diagnostics;
using Grimoire.Structs;

namespace Grimoire.Tests.Features.Moderation.Diagnostics;

internal static class DiagnosticsTestData
{
    public const ulong TestGuild = 1;
    public const ulong MuteRole = 10;
    public const ulong ModeratorRole = 20;
    public const ulong AdminRole = 30;
    public const ulong MemberRole = 40;

    public static readonly DiscordPermissions Speak = PermissionValues.LockPermissions;
    public static readonly DiscordPermissions VoiceSpeak = PermissionValues.VoiceLockPermissions;

    public static DiscordPermissions Only(DiscordPermission permission) => new(permission);

    public static IReadOnlyList<RoleSnapshot> Roles(DiscordPermissions? everyone = null) =>
    [
        new(TestGuild, "@everyone", everyone ?? Speak | VoiceSpeak),
        new(MuteRole, "Muted", DiscordPermissions.None),
        new(ModeratorRole, "Moderator", DiscordPermissions.None),
        new(AdminRole, "Admin", Only(DiscordPermission.Administrator)),
        new(MemberRole, "Member", DiscordPermissions.None)
    ];

    public static OverwriteSnapshot Role(ulong id, DiscordPermissions? allow = null, DiscordPermissions? deny = null)
        => new(id, OverwriteTarget.Role, allow ?? DiscordPermissions.None, deny ?? DiscordPermissions.None);

    public static OverwriteSnapshot Member(ulong id, DiscordPermissions allow)
        => new(id, OverwriteTarget.Member, allow, DiscordPermissions.None);

    public static ChannelSnapshot Channel(ChannelKind kind = ChannelKind.Text, bool botCanManage = true,
        params OverwriteSnapshot[] overwrites)
        => new(100, "general", kind, overwrites, botCanManage);

    /// <summary>The overwrite that <c>Mutes Create</c> puts on the mute role for a text channel.</summary>
    public static OverwriteSnapshot MuteDeny => Role(MuteRole, deny: Speak);

    public static GuildSnapshot Guild(
        IEnumerable<ChannelSnapshot> channels,
        ulong? muteRole = MuteRole,
        IReadOnlyList<RoleSnapshot>? roles = null,
        DiscordPermissions? botPermissions = null,
        int botTop = 10,
        int? muteRolePosition = 5)
        => new(TestGuild, roles ?? Roles(), muteRole, muteRolePosition,
            botPermissions ?? Only(DiscordPermission.Administrator), botTop, [.. channels]);

    public static IReadOnlySet<ulong> None => new HashSet<ulong>();
}
