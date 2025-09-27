// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain.Obsolete;

[UsedImplicitly]
[Obsolete("Use Settings Module Instead.")]
public sealed class Tracker
{
    public ulong LogChannelId { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public ulong? ModeratorId { get; set; }
    public ulong UserId { get; set; }

    public ulong GuildId { get; set; }
}
