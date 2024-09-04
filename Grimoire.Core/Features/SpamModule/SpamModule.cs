// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grimoire.Core.Features.SpamModule;
public class SpamModule
{
    public sealed record SpamUser
    {
        public required ulong UserId { get; set; }
        public required ulong GuildId { get; set; }
    }

    public sealed record SpamTracker
    {
        public int PointTotal { get; set; }
        public required string MessageCache { get; set; }
        public required DateTimeOffset DateTimeOffset { get; set; }
    }

    public sealed record CheckSpamRequest
    {
        public required ulong UserId { get; set; }
    }

    public ConcurrentDictionary<SpamUser, SpamTracker> SpamUsers { get; set; } = new ConcurrentDictionary<SpamUser, SpamTracker>();

    public bool CheckSpam(ulong UserId, ulong GuildId, int characterCount, )


}
