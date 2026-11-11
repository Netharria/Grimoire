// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;

namespace Grimoire.Extensions;

public static class PermissionsExtensions
{
    extension(DiscordPermissions permissions)
    {
        // lockPermissions is the set for the channel's type: PermissionValues.LockPermissionsFor.
        public DiscordPermissions GetLockPermissions(DiscordPermissions lockPermissions)
            => permissions & lockPermissions;

        public DiscordPermissions SetLockPermissions(DiscordPermissions lockPermissions)
            => permissions + lockPermissions;

        public DiscordPermissions RevokeLockPermissions(DiscordPermissions lockPermissions)
            => permissions - lockPermissions;

        /// <summary>
        ///     Puts the lock bits back to what they were before the lock and leaves every other bit as it is now.
        ///     Works on both the allowed and the denied side, and whether <paramref name="previousPermissions" /> holds
        ///     only the lock bits or every bit.
        /// </summary>
        public DiscordPermissions RevertLockPermissions(DiscordPermissions previousPermissions,
            DiscordPermissions lockPermissions)
            => (permissions & ~lockPermissions) | (previousPermissions & lockPermissions);

        public DiscordPermissions RevertLockPermissions(long previousPermissions, DiscordPermissions lockPermissions)
            => permissions.RevertLockPermissions(new DiscordPermissions(previousPermissions), lockPermissions);
    }
}
