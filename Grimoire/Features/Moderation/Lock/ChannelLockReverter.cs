// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;

namespace Grimoire.Features.Moderation.Lock;

internal static class ChannelLockReverter
{
    /// <summary>
    ///     Puts the @everyone overwrite's lock permissions back to what they were before the lock. Shared by the
    ///     unlock command and the expiry task so they cannot disagree.
    /// </summary>
    public static Task RevertAsync(DiscordChannel channel, DiscordRole everyoneRole, ChannelLocked activeLock)
    {
        // The @everyone overwrite should always exist, but if it does not there is nothing to keep from it.
        var current = channel.PermissionOverwrites.FirstOrDefault(x => x.Id == everyoneRole.Id);
        return channel.AddOverwriteAsync(everyoneRole,
            (current?.Allowed ?? DiscordPermissions.None).RevertLockPermissions(activeLock.PreviouslyAllowed.Permissions),
            (current?.Denied ?? DiscordPermissions.None).RevertLockPermissions(activeLock.PreviouslyDenied.Permissions));
    }
}
