// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

public sealed class Lock : Shared.IGuildChannel
{
    public required ulong ChannelId { get; init; }
    public required long PreviouslyAllowed { get; init; }
    public required long PreviouslyDenied { get; init; }
    public required ulong ModeratorId { get; set; }
    public required ulong GuildId { get; init; }
    public required string Reason { get; set; }
    public DateTimeOffset EndTime { get; set; }
}
