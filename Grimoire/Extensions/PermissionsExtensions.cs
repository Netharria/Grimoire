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
        public DiscordPermissions GetLockPermissions()
            => permissions & PermissionValues.LockPermissions;

        public DiscordPermissions SetLockPermissions()
            => permissions + PermissionValues.LockPermissions;

        public DiscordPermissions RevokeLockPermissions()
            => permissions - PermissionValues.LockPermissions;

        public DiscordPermissions RevertLockPermissions(DiscordPermissions previousPermissions)
            => permissions & (previousPermissions ^ ~PermissionValues.LockPermissions);

        public DiscordPermissions RevertLockPermissions(long previousPermissions)
            => permissions.RevertLockPermissions(new DiscordPermissions(previousPermissions));

        public DiscordPermissions SetVoiceLockPermissions()
            => permissions + PermissionValues.VoiceLockPermissions;

        public DiscordPermissions RevokeVoiceLockPermissions()
            => permissions - PermissionValues.VoiceLockPermissions;
    }

    // ReSharper disable once MemberCanBePrivate.Global


    extension(DiscordOverwrite permissions)
    {
        public PreviouslyAllowedPermissions GetPreviouslyAllowedPermissions()
            => new(long.Parse(permissions.Allowed.ToString()));

        public PreviouslyDeniedPermissions GetPreviouslyDeniedPermissions()
            => new(long.Parse(permissions.Denied.ToString()));
    }
}
