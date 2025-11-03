// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain;
using Grimoire.Settings.Domain.Shared;

namespace Grimoire.Settings.Domain;

public sealed class IgnoredChannel : IIgnored
{
    public ChannelId ChannelId { get; init; }
    public GuildId GuildId { get; init; }

    public ulong Id
    {
        get => ChannelId.Value;
        init => ChannelId = new ChannelId(value);
    }
}
