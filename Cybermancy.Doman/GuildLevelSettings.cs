// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Domain
{
    public class GuildLevelSettings
    {
        public ulong GuildId { get; set; }

        public virtual Guild Guild { get; set; } = null!;

        public bool IsLevelingEnabled { get; set; }

        public uint TextTime { get; set; }

        public uint Base { get; set; }

        public uint Modifier { get; set; }

        public uint Amount { get; set; }

        public ulong? LevelChannelLogId { get; set; }

        public virtual Channel? LevelChannelLogs { get; set; }
    }
}
