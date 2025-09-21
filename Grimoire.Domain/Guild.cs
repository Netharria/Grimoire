// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public sealed class Guild
{
    [Obsolete("Use Settings Module Instead.")]
    public ulong? ModChannelLog { get; set; }

    public Channel? ModLogChannel { get; init; }

    [Obsolete("Use Settings Module Instead.")]
    public ulong? UserCommandChannelId { get; set; }

    public Channel? UserCommandChannel { get; init; }
    public required ulong Id { get; init; }
}
