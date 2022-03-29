// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Features.Shared.SharedDtos;
using MediatR;

namespace Cybermancy.Core.Features.Shared.Commands.GuildCommands.UpdateAllGuilds
{
    public class UpdateAllGuildsCommand : IRequest
    {
        public IEnumerable<GuildDto> Guilds { get; init; } = Enumerable.Empty<GuildDto>();
        public IEnumerable<UserDto> Users { get; init; } = Enumerable.Empty<UserDto>();
        public IEnumerable<MemberDto> Members { get; init; } = Enumerable.Empty<MemberDto>();
        public IEnumerable<RoleDto> Roles { get; init; } = Enumerable.Empty<RoleDto>();
        public IEnumerable<ChannelDto> Channels { get; init; } = Enumerable.Empty<ChannelDto>();
    }
}
