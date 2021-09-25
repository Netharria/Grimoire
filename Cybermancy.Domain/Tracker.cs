// -----------------------------------------------------------------------
// <copyright file="Tracker.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System;
using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class Tracker : Identifiable
    {
        public ulong UserId { get; set; }

        public virtual User User { get; set; }

        public ulong GuildId { get; set; }

        public virtual Guild Guild { get; set; }

        public ulong LogChannelId { get; set; }

        public virtual Channel LogChannel { get; set; }

        public DateTime EndTime { get; set; }

        public ulong ModeratorId { get; set; }

        public virtual User Moderator { get; set; }
    }
}