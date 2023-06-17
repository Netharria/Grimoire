// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Queries.GetModActionsCounts
{
    public sealed record GetModActionCountsQueryResponse : BaseResponse
    {
        public int BanCount { get; init; }
        public int MuteCount { get; init; }
        public int WarnCount { get; init; }
    }
}
