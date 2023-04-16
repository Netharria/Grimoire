// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grimoire.Core.Features.Moderation.Queries.GetExpiredLocks
{
    public record GetExpiredLocksQueryResponse : BaseResponse
    {
        public ulong ChannelId { get; init; }
        public ulong GuildId { get; init; }
        public long PreviouslyAllowed { get; init; }
        public long PreviouslyDenied { get; init; }
    }
}
