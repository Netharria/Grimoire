// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

public class Reward
{
    public ulong RoleId { get; set; }

    public virtual Role Role { get; set; } = null!;

    public ulong GuildId { get; set; }

    public virtual Guild Guild { get; set; } = null!;

    public int RewardLevel { get; set; }

    public string? RewardMessage { get; set; }
}
