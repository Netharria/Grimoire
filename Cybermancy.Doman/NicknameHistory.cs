// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class NicknameHistory : Identifiable
    {
        public ulong UserId { get; set; }
        public virtual GuildUser GuildUser { get; set; } = null!;
        public string Nickname { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public ulong GuildId { get; set; }
        public virtual Guild Guild { get; set; } = null!;
    }
}
