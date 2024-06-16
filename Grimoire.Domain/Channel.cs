// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;

namespace Grimoire.Domain;

public class Channel : IIdentifiable<ulong>
{
    public ulong Id { get; set; }

    public virtual IgnoredChannel? IsIgnoredChannel { get; set; }

    public ulong GuildId { get; set; }

    public virtual Guild Guild { get; set; } = null!;

    public virtual MessageLogChannelOverride? MessageLogChannelOverride { get; set; }

    public virtual ICollection<Message> Messages { get; set; } = [];

    public virtual ICollection<OldLogMessage> OldMessages { get; set; } = [];

    public virtual ICollection<Tracker> Trackers { get; set; } = [];

    public virtual Lock? Lock { get; set; }
}
