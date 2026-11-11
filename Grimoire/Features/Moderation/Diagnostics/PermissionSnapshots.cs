// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Diagnostics;

// Plain data copied out of DSharpPlus entities, so the checks can be tested without a Discord connection.

public enum OverwriteTarget
{
    Role,
    Member
}

/// <summary>Which permission set a channel's moderation actions use: text-like (send) or voice-like (connect, speak).</summary>
public enum ChannelKind
{
    Text,
    Voice
}

public sealed record OverwriteSnapshot(
    ulong Id,
    OverwriteTarget Target,
    DiscordPermissions Allowed,
    DiscordPermissions Denied);

/// <param name="IsBotRole">
///     Managed by a bot integration (tagged with the bot's id), this bot's included. Bots need their own permissions
///     in channels, and nobody is muted or locked out through such a role, so the checks leave it out. A role a bot
///     merely holds, such as a staff role, is still checked, and so are other managed roles such as Server Booster.
/// </param>
public sealed record RoleSnapshot(ulong Id, string Name, DiscordPermissions Permissions, bool IsBotRole = false);

/// <param name="CanBeLocked">Whether <c>/Lock</c> accepts the channel. It refuses media channels.</param>
public sealed record ChannelSnapshot(
    ulong Id,
    string Name,
    ChannelKind Kind,
    IReadOnlyList<OverwriteSnapshot> Overwrites,
    bool BotCanManage,
    bool CanBeLocked = true);

/// <param name="Roles">All roles. The @everyone role has the same ID as the guild.</param>
/// <param name="MuteRoleId">The configured mute role, if any.</param>
/// <param name="BotTopRolePosition">Position of the bot's highest role.</param>
/// <param name="MuteRolePosition">Position of the mute role, if it exists.</param>
public sealed record GuildSnapshot(
    ulong GuildId,
    IReadOnlyList<RoleSnapshot> Roles,
    ulong? MuteRoleId,
    int? MuteRolePosition,
    DiscordPermissions BotPermissions,
    int BotTopRolePosition,
    IReadOnlyList<ChannelSnapshot> Channels);
