// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

public enum SpamFilterOverrideOption
{
    AlwaysFilter,
    NeverFilter
}
public class SpamFilterOverride
{
    public ulong ChannelId { get; init; }
    public virtual Channel Channel { get; init; } = null!;
    public ulong GuildId { get; init; }
    public virtual Guild Guild { get; init; } = null!;
    public SpamFilterOverrideOption ChannelOption { get; set; }
}
