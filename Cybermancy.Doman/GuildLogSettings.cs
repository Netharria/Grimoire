// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Domain
{
    public class GuildLogSettings
    {
        public ulong GuildId { get; set; }

        public virtual Guild Guild { get; set; }

        public ulong? JoinChannelLog { get; set; }

        public ulong? LeaveChannelLog { get; set; }

        public ulong? DeleteChannelLog { get; set; }

        public ulong? BulkDeleteChannelLog { get; set; }

        public ulong? EditChannelLog { get; set; }

        public ulong? UsernameChannelLog { get; set; }

        public ulong? NicknameChannelLog { get; set; }

        public ulong? AvatarChannelLog { get; set; }
        public bool IsLoggingEnabled { get; set; }
    }
}