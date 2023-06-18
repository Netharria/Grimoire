// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Leveling.Commands.ManageXpCommands.UpdateIgnoreStateForXpGain;

public sealed class UpdateIgnoreStateForXpGainCommand : ICommand<BaseResponse>
{
    public ulong GuildId { get; init; }
    public UserDto[] Users { get; set; } = Array.Empty<UserDto>();
    public RoleDto[] Roles { get; set; } = Array.Empty<RoleDto>();
    public ChannelDto[] Channels { get; set; } = Array.Empty<ChannelDto>();
    public string[] InvalidIds { get; set; } = Array.Empty<string>();
    public bool ShouldIgnore { get; init; }
}
