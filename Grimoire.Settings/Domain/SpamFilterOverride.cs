// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

public enum SpamFilterOverrideOption
{
    AlwaysFilter,
    NeverFilter,
    Inherit
}

public sealed record SpamFilterOverride(
    SpamFilterOverrideOption ChannelOption,
    ChannelId ChannelId,
    GuildId GuildId,
    ModeratorId SetBy,
    DateTimeOffset SetAt);
