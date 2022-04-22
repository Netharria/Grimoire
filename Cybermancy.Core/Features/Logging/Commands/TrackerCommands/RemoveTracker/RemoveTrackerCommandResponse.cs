// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Responses;

namespace Cybermancy.Core.Features.Logging.Commands.TrackerCommands.RemoveTracker
{
    public class RemoveTrackerCommandResponse : BaseResponse
    {
        public ulong? ModerationLogId { get; init; }
        public ulong TrackerChannelId { get; init; }
    }
}
