// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class Mute : IMember
    {
        public ulong SinId { get; set; }

        public virtual Sin Sin { get; set; } = null!;

        public ulong UserId { get; set; }

        public virtual Member Member { get; set; } = null!;

        public ulong GuildId { get; set; }

        public virtual Guild Guild { get; set; } = null!;

        public DateTime EndTime { get; set; }
    }
}
