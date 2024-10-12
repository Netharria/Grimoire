// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.SharedDtos;

public sealed record MessageDto
{
    public ulong UserId { get; init; }
    public ulong ChannelId { get; init; }
    public ulong MessageId { get; init; }
    public string MessageContent { get; init; } = string.Empty;
    public AttachmentDto[] Attachments { get; init; } = [];
}
