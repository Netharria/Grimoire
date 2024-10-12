// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Extensions;


public static class PermissionsExtensions
{
    public static DiscordPermissions GetLockPermissions(this DiscordPermissions permissions)
        => permissions & PermissionValues.LockPermissions;

    public static DiscordPermissions SetLockPermissions(this DiscordPermissions permissions)
        => permissions | PermissionValues.LockPermissions;
    public static DiscordPermissions RevokeLockPermissions(this DiscordPermissions permissions)
        => permissions & ~PermissionValues.LockPermissions;
    public static DiscordPermissions RevertLockPermissions(this DiscordPermissions permissions, DiscordPermissions previousPermissions)
        => permissions & (previousPermissions ^ ~PermissionValues.LockPermissions);

    public static DiscordPermissions RevertLockPermissions(this DiscordPermissions permissions, long previousPermissions)
        => permissions.RevertLockPermissions((DiscordPermissions)previousPermissions);


    public static DiscordPermissions SetVoiceLockPermissions(this DiscordPermissions permissions)
        => permissions | PermissionValues.VoiceLockPermissions;
    public static DiscordPermissions RevokeVoiceLockPermissions(this DiscordPermissions permissions)
        => permissions & ~PermissionValues.VoiceLockPermissions;

    public static long ToLong(this DiscordPermissions permissions)
        => (long)permissions;
}
