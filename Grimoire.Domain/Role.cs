// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;

namespace Grimoire.Domain
{
    public class Role : IIdentifiable<ulong>, IXpIgnore, IMentionable
    {
        public ulong Id { get; set; }

        public ulong GuildId { get; set; }

        public virtual Guild Guild { get; set; } = null!;

        public virtual Reward? Reward { get; set; }

        public bool IsXpIgnored { get; set; }
    }
}
