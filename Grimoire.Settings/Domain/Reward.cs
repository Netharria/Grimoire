// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

public sealed class Reward
{
    public required ulong RoleId { get; init; }

    public required ulong GuildId { get; init; }
    public int RewardLevel { get; set; }
    public string? RewardMessage { get; set; }
}
