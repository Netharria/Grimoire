// -----------------------------------------------------------------------
// <copyright file="UserLevel.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System;
using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class UserLevel : Identifiable, IXpIgnore
    {
        public ulong GuildId { get; set; }

        public virtual Guild Guild { get; set; }

        public ulong UserId { get; set; }

        public virtual User User { get; set; }

        public int Xp { get; set; }

        public DateTime TimeOut { get; set; }

        public bool IsXpIgnored { get; set; }
    }
}