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

        public int TextTime { get; set; }

        public int Base { get; set; }

        public int Modifier { get; set; }

        public int Amount { get; set; }

        public ulong? LevelChannelLog { get; set; }
    }
}
