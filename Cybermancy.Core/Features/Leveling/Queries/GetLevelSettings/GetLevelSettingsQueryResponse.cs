// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Core.Features.Leveling.Queries.GetLevelSettings
{
    public sealed record GetLevelSettingsQueryResponse : BaseResponse
    {
        public bool ModuleEnabled { get; init; }
        public TimeSpan TextTime { get; init; }
        public int Base { get; init; }
        public int Modifier { get; init; }
        public int Amount { get; init; }
        public ulong? LevelChannelLog { get; init; }
    }
}
