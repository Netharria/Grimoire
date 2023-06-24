// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Discord.Extensions;

public static class PermissionsExtensions
{
    public static Permissions GetLockPermissions(this Permissions permissions)
        => permissions & PermissionValues.LockPermissions;

    public static Permissions SetLockPermissions(this Permissions permissions)
        => permissions | PermissionValues.LockPermissions;
    public static Permissions RevokeLockPermissions(this Permissions permissions)
        => permissions & ~PermissionValues.LockPermissions;
    public static Permissions RevertLockPermissions(this Permissions permissions, Permissions previousPermissions)
        => permissions & (previousPermissions ^ ~PermissionValues.LockPermissions);

    public static Permissions RevertLockPermissions(this Permissions permissions, long previousPermissions)
        => permissions.RevertLockPermissions((Permissions)previousPermissions);


    public static Permissions SetVoiceLockPermissions(this Permissions permissions)
        => permissions | PermissionValues.VoiceLockPermissions;
    public static Permissions RevokeVoiceLockPermissions(this Permissions permissions)
        => permissions & ~PermissionValues.VoiceLockPermissions;

    public static long ToLong(this Permissions permissions)
        => (long)permissions;
}
