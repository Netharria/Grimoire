// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.MessageLoggingCommands.UpdateMessage
{
    public sealed record UpdateMessageCommandResponse : BaseResponse
    {
        public ulong MessageId { get; init; }
        public ulong? UpdateMessageLogChannelId { get; init; }
        public string? MessageContent { get; init; }
        public ulong UserId { get; init; }
        public bool Success { get; init; } = false;
    }
}
