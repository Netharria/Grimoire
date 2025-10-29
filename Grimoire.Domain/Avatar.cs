// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public sealed class Avatar
{
    public required string FileName { get; init; }
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    public required ulong UserId { get; init; }
    public required ulong GuildId { get; init; }
}
