// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Shared.Commands.GuildCommands.UpdateAllGuilds
{
    public sealed record UpdateAllGuildsCommand : ICommand
    {
        public IEnumerable<GuildDto> Guilds { get; init; } = Enumerable.Empty<GuildDto>();
        public IEnumerable<UserDto> Users { get; init; } = Enumerable.Empty<UserDto>();
        public IEnumerable<MemberDto> Members { get; init; } = Enumerable.Empty<MemberDto>();
        public IEnumerable<RoleDto> Roles { get; init; } = Enumerable.Empty<RoleDto>();
        public IEnumerable<ChannelDto> Channels { get; init; } = Enumerable.Empty<ChannelDto>();
        public IEnumerable<Invite> Invites { get; init; } = Enumerable.Empty<Invite>();
    }
}
