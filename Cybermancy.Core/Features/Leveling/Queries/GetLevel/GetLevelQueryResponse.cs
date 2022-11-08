// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Responses;

namespace Cybermancy.Core.Features.Leveling.Queries.GetLevel
{
    public sealed record GetLevelQueryResponse : BaseResponse
    {
        public long UsersXp { get; init; }
        public int UsersLevel { get; init; }
        public long LevelProgress { get; init; }
        public long XpForNextLevel { get; init; }
        public ulong? NextRoleRewardId { get; init; }
        public int? NextRewardLevel { get; init; }
    }
}
