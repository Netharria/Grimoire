// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain;

namespace Grimoire.Settings.Domain;

public sealed class Tracker
{
    public required ChannelId LogChannelId { get; set; }
    public required DateTimeOffset EndTime { get; set; }
    public required ModeratorId ModeratorId { get; set; }
    public required UserId UserId { get; set; }
    public required GuildId GuildId { get; set; }
}
