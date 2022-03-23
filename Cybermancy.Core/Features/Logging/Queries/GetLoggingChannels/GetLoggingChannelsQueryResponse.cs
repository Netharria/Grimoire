// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Responses;

namespace Cybermancy.Core.Features.Logging.Queries.GetLoggingChannels
{
    public class GetLoggingChannelsQueryResponse : BaseResponse
    {
        public ulong? JoinChannelLogId { get; init; }

        public ulong? LeaveChannelLogId { get; init; }

        public ulong? DeleteChannelLogId { get; init; }

        public ulong? BulkDeleteChannelLogId { get; init; }

        public ulong? EditChannelLogId { get; init; }

        public ulong? UsernameChannelLogId { get; init; }

        public ulong? NicknameChannelLogId { get; init; }

        public ulong? AvatarChannelLogId { get; init; }
    }
}
