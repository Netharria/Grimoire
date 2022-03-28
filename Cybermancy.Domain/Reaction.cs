// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Domain
{
    public class Reaction
    {
        public ulong MessageId { get; set; }

        public virtual Message Message { get; set; } = null!;

        public ulong GuildUserId { get; set; }

        public virtual GuildUser GuildUser { get; set; } = null!;

        public ulong GuildId { get; set; }

        public virtual Guild Guild { get; set; } = null!;

        public ulong EmojiId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;
    }
}
