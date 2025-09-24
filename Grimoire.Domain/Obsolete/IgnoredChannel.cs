// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain.Obsolete;

[UsedImplicitly]
[Obsolete("Use Settings Module Instead.")]
public sealed class IgnoredChannel
{
    public ulong ChannelId { get; init; }
    public Channel? Channel { get; init; }
    public ulong GuildId { get; init; }
    public Guild? Guild { get; init; }
}
