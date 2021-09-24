// -----------------------------------------------------------------------
// <copyright file="Reward.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

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