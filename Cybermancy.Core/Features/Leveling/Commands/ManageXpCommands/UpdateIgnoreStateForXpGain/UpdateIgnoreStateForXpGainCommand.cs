// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Features.Shared.SharedDtos;
using MediatR;

namespace Cybermancy.Core.Features.Leveling.Commands.UpdateIgnoreStateForXpGain
{
    public class UpdateIgnoreStateForXpGainCommand : IRequest<UpdateIgnoreStateForXpGainResponse>
    {
        public ulong GuildId { get; set; }
        public IEnumerable<UserDto> Users { get; set; } = Array.Empty<UserDto>();
        public ulong[] RoleIds { get; set; } = Array.Empty<ulong>();
        public ulong[] ChannelIds { get; set; } = Array.Empty<ulong>();
        public string[] InvalidIds { get; set; } = Array.Empty<string>();
        public bool ShouldIgnore { get; set; }
    }
}
