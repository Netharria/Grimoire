// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Structs;

public struct PermissionValues
{
    public static DiscordPermissions LockPermissions
        => new DiscordPermissions(new []{DiscordPermission.SendMessages, DiscordPermission.SendThreadMessages,
            DiscordPermission.SendTtsMessages, DiscordPermission.AddReactions});

    public static DiscordPermissions VoiceLockPermissions
        => new DiscordPermissions(new[] { DiscordPermission.Connect, DiscordPermission.Speak });

}
