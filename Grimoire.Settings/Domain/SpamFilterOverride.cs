// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

public enum SpamFilterOverrideOption
{
    AlwaysFilter,
    NeverFilter
}

public sealed record SpamFilterOverride
{
    public SpamFilterOverrideOption ChannelOption { get; set; }
    public required ChannelId ChannelId { get; init; }
    public required GuildId GuildId { get; init; }
}
