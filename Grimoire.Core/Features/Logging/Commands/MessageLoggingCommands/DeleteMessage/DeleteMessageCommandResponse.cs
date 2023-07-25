// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.MessageLoggingCommands.DeleteMessage;

public sealed record DeleteMessageCommandResponse : BaseResponse
{
    public ulong? LoggingChannel { get; init; }
    public ulong UserId { get; init; }
    public string? MessageContent { get; init; }
    public ulong? ReferencedMessage { get; init; }
    public AttachmentDto[] Attachments { get; init; } = Array.Empty<AttachmentDto>();
    public bool Success { get; init; }
}
