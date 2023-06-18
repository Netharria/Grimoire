// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;

namespace Grimoire.Domain;

public class Channel : IIdentifiable<ulong>, IXpIgnore, IMentionable
{
    public ulong Id { get; set; }

    public bool IsXpIgnored { get; set; }

    public ulong GuildId { get; set; }

    public virtual Guild Guild { get; set; } = null!;

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual ICollection<OldLogMessage> OldMessages { get; set; } = new HashSet<OldLogMessage>();

    public virtual ICollection<Tracker> Trackers { get; set; } = new List<Tracker>();

    public virtual Lock? Lock { get; set; }
}
