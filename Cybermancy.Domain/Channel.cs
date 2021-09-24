// -----------------------------------------------------------------------
// <copyright file="Channel.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Domain
{
    using System.Collections.Generic;
    using Cybermancy.Domain.Shared;

    public class Channel : Identifiable, IXpIgnore
    {
        public string Name { get; set; }

        public bool IsXpIgnored { get; set; }

        public ulong GuildId { get; set; }

        public virtual Guild Guild { get; set; }

        public virtual ICollection<Message> Messages { get; set; }

        public virtual ICollection<Tracker> Trackers { get; set; }

        public virtual Lock Lock { get; set; }
    }
}