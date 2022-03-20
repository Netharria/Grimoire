// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Core.Features.Logging.Queries.GetOldLogMessages
{
    public class GetOldLogMessagesQueryResponse
    {
        public IEnumerable<OldLogMessageChannel> Channels { get; init; } = Enumerable.Empty<OldLogMessageChannel>();
    }

    public class OldLogMessageChannel
    {
        public ulong ChannelId { get; init; }
        public ulong[] MessageIds { get; init; } = Array.Empty<ulong>();
    }
}
