// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Domain.Obsolete;

[Obsolete("Use Settings Module Instead.")]
public enum SpamFilterOverrideOption
{
    AlwaysFilter,
    NeverFilter
}

[Obsolete("Use Settings Module Instead.")]
public sealed class SpamFilterOverride
{
    public ulong ChannelId { get; init; }
    public ulong GuildId { get; init; }
    public SpamFilterOverrideOption ChannelOption { get; set; }
}
