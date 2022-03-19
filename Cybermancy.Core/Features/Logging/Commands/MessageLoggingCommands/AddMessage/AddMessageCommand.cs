// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using MediatR;

namespace Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.AddMessage
{
    public class AddMessageCommand : IRequest
    {
        public ulong MessageId { get; init; }
        public ulong ChannelId { get; init; }
        public string MessageContent { get; init; } = string.Empty;
        public ulong AuthorId { get; init; }
        public ICollection<string> Attachments { get; init; } = new List<string>();
        public DateTime CreatedTimestamp { get; init; }
        public ulong? ReferencedMessageId { get; init; }
    }
}
