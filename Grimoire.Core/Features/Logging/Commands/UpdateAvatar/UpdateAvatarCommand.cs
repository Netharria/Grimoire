// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.UpdateAvatar;

public sealed record UpdateAvatarCommand : ICommand<UpdateAvatarCommandResponse>
{
    public ulong UserId { get; init; }
    public ulong GuildId { get; init; }
    public string AvatarUrl { get; init; } = string.Empty;
}
