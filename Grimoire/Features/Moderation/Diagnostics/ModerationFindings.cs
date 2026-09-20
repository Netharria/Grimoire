// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Diagnostics;

public enum GuildFindingKind
{
    MuteRoleNotConfigured,
    MuteRoleNotFound,
    BotMissingManageRoles,
    MuteRoleAboveBot,
    BotMissingManageChannels
}

public enum ChannelFindingKind
{
    /// <summary>A member holding only the mute role can still use the channel.</summary>
    MuteRoleNotDenied,

    /// <summary>Members with these roles can still use the channel while muted.</summary>
    RoleCanSpeakWhenMuted,

    /// <summary>These members have their own overwrite that lets them use the channel while muted.</summary>
    MemberCanSpeakWhenMuted,

    /// <summary>Members with these roles can still use the channel while it is locked.</summary>
    RoleCanSpeakWhenLocked,

    /// <summary>These members have their own overwrite that lets them use the channel while it is locked.</summary>
    MemberCanSpeakWhenLocked,

    /// <summary>The bot does not have Manage Channels in this channel, so it cannot lock it.</summary>
    BotCannotLock
}

public sealed record GuildFinding(GuildFindingKind Kind);

/// <param name="SubjectIds">Role or member IDs the finding is about; empty when it applies to the channel itself.</param>
public sealed record ChannelFinding(
    ChannelSnapshot Channel,
    ChannelFindingKind Kind,
    IReadOnlyList<ulong> SubjectIds);

/// <param name="IgnoredMemberIds">Members whose own overwrites were excluded on request.</param>
/// <param name="AdministratorRoles">Excluded automatically, because Administrator ignores overwrites.</param>
/// <param name="IgnoredRoles">Excluded because the moderator asked for it, e.g. moderators who may speak in locked channels.</param>
public sealed record DiagnosticsResult(
    IReadOnlyList<RoleSnapshot> AdministratorRoles,
    IReadOnlyList<RoleSnapshot> IgnoredRoles,
    IReadOnlyList<ulong> IgnoredMemberIds,
    IReadOnlyList<GuildFinding> GuildFindings,
    IReadOnlyList<ChannelFinding> ChannelFindings)
{
    public bool HasFindings => this.GuildFindings.Count > 0 || this.ChannelFindings.Count > 0;
}
