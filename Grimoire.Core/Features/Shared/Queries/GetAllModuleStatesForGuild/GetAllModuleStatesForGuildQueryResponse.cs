// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Shared.Queries.GetAllModuleStatesForGuild
{
    public sealed record GetAllModuleStatesForGuildQueryResponse : BaseResponse
    {
        public bool LevelingIsEnabled { get; init; }
        public bool UserLogIsEnabled { get; init; }
        public bool ModerationIsEnabled { get; init; }
        public bool MessageLogIsEnabled { get; init; }
    }
}
