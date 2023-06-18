// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.MessageLoggingCommands.AddMessage;

public sealed record AddMessageCommand : ICommand
{
    public ulong MessageId { get; init; }
    public ulong ChannelId { get; init; }
    public string MessageContent { get; init; } = string.Empty;
    public ulong UserId { get; init; }
    public AttachmentDto[] Attachments { get; init; } = Array.Empty<AttachmentDto>();
    public ulong? ReferencedMessageId { get; init; }
    public ulong GuildId { get; init; }
}
