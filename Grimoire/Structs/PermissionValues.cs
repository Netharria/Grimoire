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
        => new(DiscordPermission.SendMessages, DiscordPermission.SendThreadMessages, DiscordPermission.SendTtsMessages,
            DiscordPermission.AddReactions);

    /// <summary>
    ///     What a lock or a mute denies on voice and stage channels. They have a text chat too, so this also denies
    ///     what the text set denies, apart from Send Thread Messages: they have no threads (KI-56).
    /// </summary>
    public static DiscordPermissions VoiceLockPermissions
        => new(DiscordPermission.Connect, DiscordPermission.Speak, DiscordPermission.SendMessages,
            DiscordPermission.SendTtsMessages, DiscordPermission.AddReactions);

    /// <summary>
    ///     The set a lock denies on a channel of this type: the voice set on voice and stage channels, and both sets on
    ///     a category, whose overwrite reaches the text and voice channels synced to it.
    /// </summary>
    public static DiscordPermissions LockPermissionsFor(DiscordChannelType channelType)
        => channelType switch
        {
            DiscordChannelType.Voice or DiscordChannelType.Stage => VoiceLockPermissions,
            DiscordChannelType.Category => LockPermissions | VoiceLockPermissions,
            _ => LockPermissions
        };
}
