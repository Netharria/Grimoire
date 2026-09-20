// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Utilities;

public enum ExpiredItemAction
{
    /// <summary>The bot is not in the guild. Keep the record, since its settings may be needed if the bot returns.</summary>
    KeepForLater,

    /// <summary>The bot is in the guild but what the record refers to is gone, so there is nothing left to do.</summary>
    Resolve,

    /// <summary>Carry out the expiry.</summary>
    Process
}

/// <summary>
///     Decides what to do with an expired mute or lock. Without this, records for guilds the bot left or targets that
///     were deleted are retried against Discord on every tick, forever.
/// </summary>
public static class ExpiredItemTriage
{
    public static ExpiredItemAction For(bool botIsInGuild, bool targetExists) => (botIsInGuild, targetExists) switch
    {
        (false, _) => ExpiredItemAction.KeepForLater,
        (true, false) => ExpiredItemAction.Resolve,
        (true, true) => ExpiredItemAction.Process
    };
}
