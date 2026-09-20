// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

/// <summary>
///     A role or member that the moderation diagnostics leave out, for example moderators who are expected to be
///     able to speak in locked channels.
/// </summary>
public abstract record DiagnosticsIgnoredItem
{
    protected ulong Id { get; init; }
    public required GuildId GuildId { get; init; }
    public required ModeratorId SetBy { get; init; }
    public required DateTimeOffset SetAt { get; init; }
}

public sealed record DiagnosticsIgnoredRole : DiagnosticsIgnoredItem
{
    public required RoleId RoleId { get => new(this.Id); init => this.Id = value.Value; }

    public static Validation<DiagnosticsIgnoredRole> Create(
        RoleId roleId, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
    {
        if (roleId.Value == 0)
            return Validation<DiagnosticsIgnoredRole>.Fail(
                new Error("diagnostics-ignored-role.role-id.invalid", "RoleId must be specified."));
        if (guildId.Value == 0)
            return Validation<DiagnosticsIgnoredRole>.Fail(
                new Error("diagnostics-ignored-role.guild-id.invalid", "GuildId must be specified."));
        if (setBy.Value == 0)
            return Validation<DiagnosticsIgnoredRole>.Fail(
                new Error("diagnostics-ignored-role.set-by.invalid", "ModeratorId must be specified."));
        return Validation<DiagnosticsIgnoredRole>.Succeed(
            new DiagnosticsIgnoredRole { RoleId = roleId, GuildId = guildId, SetBy = setBy, SetAt = setAt });
    }
}

public sealed record DiagnosticsIgnoredMember : DiagnosticsIgnoredItem
{
    public required UserId UserId { get => new(this.Id); init => this.Id = value.Value; }

    public static Validation<DiagnosticsIgnoredMember> Create(
        UserId userId, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
    {
        if (userId.Value == 0)
            return Validation<DiagnosticsIgnoredMember>.Fail(
                new Error("diagnostics-ignored-member.user-id.invalid", "UserId must be specified."));
        if (guildId.Value == 0)
            return Validation<DiagnosticsIgnoredMember>.Fail(
                new Error("diagnostics-ignored-member.guild-id.invalid", "GuildId must be specified."));
        if (setBy.Value == 0)
            return Validation<DiagnosticsIgnoredMember>.Fail(
                new Error("diagnostics-ignored-member.set-by.invalid", "ModeratorId must be specified."));
        return Validation<DiagnosticsIgnoredMember>.Succeed(
            new DiagnosticsIgnoredMember { UserId = userId, GuildId = guildId, SetBy = setBy, SetAt = setAt });
    }
}
