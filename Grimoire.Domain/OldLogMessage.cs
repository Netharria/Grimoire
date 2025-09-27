// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public sealed class OldLogMessage
{
    public required ulong ChannelId { get; init; }

    public required ulong GuildId { get; init; }

    public DateTimeOffset CreatedAt { get; private init; }

    public int TimesTried { get; init; }
    public required ulong Id { get; init; }
}
