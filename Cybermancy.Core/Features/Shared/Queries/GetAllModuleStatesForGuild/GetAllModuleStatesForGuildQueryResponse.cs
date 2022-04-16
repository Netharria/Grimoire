// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Responses;

namespace Cybermancy.Core.Features.Shared.Queries.GetAllModuleStatesForGuild
{
    public class GetAllModuleStatesForGuildQueryResponse : BaseResponse
    {
        public bool LevelingIsEnabled { get; init; }
        public bool LoggingIsEnabled { get; init; }
        public bool ModerationIsEnabled { get; init; }
    }
}
