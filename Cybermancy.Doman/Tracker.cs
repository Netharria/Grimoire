// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Domain
{
    public class Tracker
    {
        public ulong GuildUserId { get; set; }

        public virtual GuildUser GuildUser { get; set; } = null!;

        public ulong GuildId { get; set; }

        public virtual Guild Guild { get; set; } = null!;

        public ulong LogChannelId { get; set; }

        public virtual Channel LogChannel { get; set; } = null!;

        public DateTime EndTime { get; set; }

        public ulong ModeratorId { get; set; }

        public virtual GuildUser Moderator { get; set; } = null!;
    }
}
