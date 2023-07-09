// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.UpdateUsername;

public sealed record UpdateUsernameCommandResponse : BaseResponse
{
    public string BeforeUsername { get; init; } = string.Empty;
    public string AfterUsername { get; init; } = string.Empty;
    public ulong? UsernameChannelLogId { get; init; }
}
