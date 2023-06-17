// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Enums;

namespace Grimoire.Core.Features.Shared.Queries.GetModuleStateForGuild
{
    public class GetModuleStateForGuildQuery : IRequest<bool>
    {
        public ulong GuildId { get; init; }
        public Module Module { get; init; }
    }
}