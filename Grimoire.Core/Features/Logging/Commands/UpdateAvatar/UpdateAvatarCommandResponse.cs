// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.UpdateAvatar;

public sealed record UpdateAvatarCommandResponse : BaseResponse
{
    public string BeforeAvatar { get; init; } = string.Empty;
    public string AfterAvatar { get; init; } = string.Empty;
    public ulong AvatarChannelLogId { get; init; }
}
