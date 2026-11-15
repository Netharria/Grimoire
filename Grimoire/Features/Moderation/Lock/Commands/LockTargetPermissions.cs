// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using LanguageExt;

namespace Grimoire.Features.Moderation.Lock.Commands;

internal static class LockTargetPermissions
{
    /// <summary>
    ///     Discord checks the moderator only in the channel the command is run in, so without this a moderator of one
    ///     channel could lock or unlock any other. Channel-level on purpose: channel moderators may lock their own.
    /// </summary>
    public static Fin<Unit> CheckModerator(DiscordMember moderator, DiscordChannel channel)
        => channel.PermissionsFor(moderator).HasPermission(DiscordPermission.ManageMessages)
            ? Fin.Succ<Unit>(unit)
            : new Invalid($"You need **Manage Messages** in {channel.Mention} to lock or unlock it.");
}
