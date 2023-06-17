// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Moderation.Queries.GetBan;

namespace Grimoire.Core.Features.Moderation.Queries.GetUnban
{
    public sealed record GetUnbanQuery : IRequest<GetBanQueryResponse>
    {
        public long SinId { get; init; }
        public ulong GuildId { get; init; }
    }
}