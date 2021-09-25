// -----------------------------------------------------------------------
// <copyright file="Role.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class Role : Identifiable, IXpIgnore
    {
        public ulong GuildId { get; set; }

        public virtual Guild Guild { get; set; }

        public virtual Reward Reward { get; set; }

        public bool IsXpIgnored { get; set; }
    }
}