// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


namespace Grimoire.Domain.Enums;

[Flags]
public enum CommandPermissions : long
{
    None = 0,
    ManageXp = 1,
    Ban = 1 << 2,
    Forget = 1 << 3,
    Ignore = 1 << 4,
    Leaderboard = 1 << 5,
    Level = 1 << 6,
    Lock = 1 << 7,
    Mute = 1 << 8,
    Pardon = 1 << 9,
    PublishBan = 1 << 10,
    Purge = 1 << 11,
    SinReason = 1 << 12,
    Rewards = 1 << 13,
    SinLogSelf = 1 << 14,
    SinLogAll = 1 << 15,
    TrackUser = 1 << 16,
    Warn = 1 << 17
}
