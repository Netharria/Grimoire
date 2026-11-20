// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Values;

namespace Grimoire.Settings.Domain;

/// <summary>A role given to members once they reach a level, with an optional message sent to them by DM.</summary>
public sealed record RewardEntry(RoleId RoleId, int RewardLevel, RewardMessage? RewardMessage)
{
    /// <summary>The lowest level a reward can be given at (Q41b). Every member is at least level 1.</summary>
    public const int MinimumLevel = 1;

    public static Validation<Error, RewardEntry> Create(RoleId roleId, int level, RewardMessage? message)
        => level < MinimumLevel
            ? new Invalid($"Reward Level must be at least {MinimumLevel}.")
            : Success<Error, RewardEntry>(new RewardEntry(roleId, level, message));
}
