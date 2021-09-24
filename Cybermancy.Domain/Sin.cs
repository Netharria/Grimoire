﻿// -----------------------------------------------------------------------
// <copyright file="Sin.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Domain
{
    using System;
    using System.Collections.Generic;
    using Cybermancy.Domain.Shared;

    public enum SinType
    {
        Warn,
        Mute,
        Ban,
    }

    public class Sin : Identifiable
    {
        public ulong UserId { get; set; }

        public virtual User User { get; set; }

        public ulong ModeratorId { get; set; }

        public virtual User Moderator { get; set; }

        public ulong GuildId { get; set; }

        public virtual Guild Guild { get; set; }

        public string Reason { get; set; }

        public DateTime InfractionOn { get; set; }

        public SinType SinType { get; set; }

        public virtual Mute Mute { get; set; }

        public virtual Pardon Pardon { get; set; }

        public virtual ICollection<PublishedMessage> PublishMessages { get; set; }
    }
}