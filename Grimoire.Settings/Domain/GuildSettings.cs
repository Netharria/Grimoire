// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain;

namespace Grimoire.Settings.Domain;

public sealed class GuildSettings
{
    public required GuildId Id { get; init; }
    public ChannelId? ModLogChannelId { get; set; }
    public ChannelId? UserCommandChannelId { get; set; }
}
