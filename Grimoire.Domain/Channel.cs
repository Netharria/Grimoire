// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;
using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public class Channel : IIdentifiable<ulong>
{
    public virtual IgnoredChannel? IsIgnoredChannel { get; init; }

    public ulong GuildId { get; init; }

    public virtual Guild Guild { get; init; } = null!;

    public virtual MessageLogChannelOverride? MessageLogChannelOverride { get; init; }

    public virtual SpamFilterOverride? SpamFilterOverride { get; init; }

    public virtual ICollection<Message> Messages { get; init; } = [];

    public virtual ICollection<OldLogMessage> OldMessages { get; init; } = [];

    public virtual ICollection<Tracker> Trackers { get; init; } = [];

    public virtual Lock? Lock { get; init; }
    public ulong Id { get; set; }
}
