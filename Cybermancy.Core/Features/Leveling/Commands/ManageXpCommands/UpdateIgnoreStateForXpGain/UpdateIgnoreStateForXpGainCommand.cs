// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Features.Shared.SharedDtos;
using Cybermancy.Core.Responses;
using Mediator;

namespace Cybermancy.Core.Features.Leveling.Commands.ManageXpCommands.UpdateIgnoreStateForXpGain
{
    public sealed class UpdateIgnoreStateForXpGainCommand : ICommand<BaseResponse>
    {
        public ulong GuildId { get; init; }
        public UserDto[] Users { get; init; } = Array.Empty<UserDto>();
        public RoleDto[] Roles { get; init; } = Array.Empty<RoleDto>();
        public ChannelDto[] Channels { get; init; } = Array.Empty<ChannelDto>();
        public string[] InvalidIds { get; init; } = Array.Empty<string>();
        public bool ShouldIgnore { get; init; }
    }
}
