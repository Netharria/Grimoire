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
public sealed class SpamFilterOverride : Shared.IGuildChannel
{
    public required ulong ChannelId { get; init; }
    public required ulong GuildId { get; init; }
    public SpamFilterOverrideOption ChannelOption { get; set; }
}
