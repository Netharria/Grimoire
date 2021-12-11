// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Domain
{
    public class Reward
    {
        public ulong GuildId { get; set; }

        public virtual Guild Guild { get; set; }

        public ulong RoleId { get; set; }

        public virtual Role Role { get; set; }

        public int RewardLevel { get; set; }
    }
}