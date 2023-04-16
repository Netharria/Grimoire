// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Domain
{
    public class Lock
    {
        public ulong ChannelId { get; set; }

        public virtual Channel Channel { get; set; } = null!;

        public long PreviouslyAllowed { get; set; }

        public long PreviouslyDenied { get; set; }

        public ulong ModeratorId { get; set; }

        public virtual Member Moderator { get; set; } = null!;

        public ulong GuildId { get; set; }

        public virtual Guild Guild { get; set; } = null!;

        public string Reason { get; set; } = string.Empty;

        public DateTimeOffset EndTime { get; set; }
    }
}
