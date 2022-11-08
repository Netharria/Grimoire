// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Responses;

namespace Cybermancy.Core.Features.Logging.Queries.GetLogSettings
{
    public sealed record GetLoggingSettingsQueryResponse : BaseResponse
    {
        public ulong? JoinChannelLog { get; init; }
        public ulong? LeaveChannelLog { get; init; }
        public ulong? DeleteChannelLog { get; init; }
        public ulong? BulkDeleteChannelLog { get; init; }
        public ulong? EditChannelLog { get; init; }
        public ulong? UsernameChannelLog { get; init; }
        public ulong? NicknameChannelLog { get; init; }
        public ulong? AvatarChannelLog { get; init; }
        public bool IsLoggingEnabled { get; init; }
    }
}
