// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Queries.GetUserLogSettings;

public sealed record GetUserLogSettingsQueryResponse : BaseResponse
{
    public ulong? JoinChannelLog { get; init; }
    public ulong? LeaveChannelLog { get; init; }
    public ulong? UsernameChannelLog { get; init; }
    public ulong? NicknameChannelLog { get; init; }
    public ulong? AvatarChannelLog { get; init; }
    public bool IsLoggingEnabled { get; init; }
}
