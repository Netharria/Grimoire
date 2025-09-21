// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

public sealed class Tracker
{
    public GuildSettings? GuildSettings { get; init; }
    public required ulong LogChannelId { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public required ulong ModeratorId { get; set; }
    public required ulong UserId { get; set; }
    public required ulong GuildId { get; set; }
}
