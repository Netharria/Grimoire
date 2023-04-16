// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Core.Features.Moderation.Commands.LockCommands.LockChannel
{
    public record LockChannelCommand : ICommand<BaseResponse>
    {
        public ulong ChannelId { get; init; }
        public long PreviouslyAllowed { get; init; }
        public long PreviouslyDenied { get; init; }
        public ulong ModeratorId { get; init; }
        public ulong GuildId { get; init; }
        public string Reason { get; init; } = string.Empty;
        public DurationType DurationType { get; init; }
        public long DurationAmount { get; init; }
    }
}
