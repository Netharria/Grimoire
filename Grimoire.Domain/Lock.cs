// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public class Lock
{
    public ulong ChannelId { get; init; }

    public virtual Channel Channel { get; init; } = null!;

    public long PreviouslyAllowed { get; init; }

    public long PreviouslyDenied { get; init; }

    public ulong? ModeratorId { get; set; }

    public virtual Member? Moderator { get; init; }

    public ulong GuildId { get; init; }

    public virtual Guild Guild { get; init; } = null!;

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset EndTime { get; set; }
}
