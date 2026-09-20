// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Diagnostics;

/// <summary>Discord's permission resolution order for a hypothetical member holding a given set of roles.</summary>
public static class EffectivePermissions
{
    /// <param name="heldRoleIds">The member's roles, not counting @everyone which everyone has.</param>
    public static DiscordPermissions For(
        IReadOnlyList<RoleSnapshot> roles,
        ulong guildId,
        ChannelSnapshot channel,
        IReadOnlySet<ulong> heldRoleIds)
    {
        var basePermissions = roles
            .Where(role => role.Id == guildId || heldRoleIds.Contains(role.Id))
            .Aggregate(DiscordPermissions.None, (acc, role) => acc | role.Permissions);

        if (basePermissions.HasPermission(DiscordPermission.Administrator))
            return DiscordPermissions.All;

        var permissions = basePermissions;

        if (channel.Overwrites.FirstOrDefault(o => o is { Target: OverwriteTarget.Role } && o.Id == guildId)
            is { } everyone)
            permissions = (permissions & ~everyone.Denied) | everyone.Allowed;

        // Role overwrites are combined first: every deny is applied, then every allow. So an allow beats a deny
        // that comes from a different role.
        var held = channel.Overwrites
            .Where(o => o is { Target: OverwriteTarget.Role } && o.Id != guildId && heldRoleIds.Contains(o.Id))
            .ToList();
        var deny = held.Aggregate(DiscordPermissions.None, (acc, o) => acc | o.Denied);
        var allow = held.Aggregate(DiscordPermissions.None, (acc, o) => acc | o.Allowed);

        return (permissions & ~deny) | allow;
    }
}
