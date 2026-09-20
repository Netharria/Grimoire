// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Mute;

public static class MuteRoleResolver
{
    /// <summary>
    ///     Picks the role to take off (or give back to) a muted member: the role that was recorded when the member was
    ///     muted, or if that role no longer exists the server's current mute role. <c>null</c> when neither exists,
    ///     which means there is nothing to remove.
    /// </summary>
    public static RoleId? Resolve(RoleId? recordedRole, RoleId? configuredRole, Func<RoleId, bool> roleExists)
        => recordedRole is { } recorded && roleExists(recorded) ? recorded
            : configuredRole is { } configured && roleExists(configured) ? configured
            : null;
}
