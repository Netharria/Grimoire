// -----------------------------------------------------------------------
// <copyright file="Message.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Domain
{
    using System;
    using System.Collections.Generic;
    using Cybermancy.Domain.Shared;

    public class Message : Identifiable
    {
        public ulong UserId { get; set; }

        public virtual User User { get; set; }

        public virtual ulong ChannelId { get; set; }

        public virtual Channel Channel { get; set; }

        public ulong GuildId { get; set; }

        public virtual Guild Guild { get; set; }

        public string Content { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual ICollection<Attachment> Attachments { get; set; }
    }
}