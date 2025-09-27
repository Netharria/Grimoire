// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

public enum XpHistoryType
{
    Earned,
    Awarded,
    Reclaimed,
    [UsedImplicitly] Migrated,
    Created
}

[UsedImplicitly]
public sealed class XpHistory
{
    public required long Xp { get; init; }
    public required DateTimeOffset TimeOut { get; init; }
    public required XpHistoryType Type { get; init; }
    public ulong? AwarderId { get; init; }
    public required ulong UserId { get; init; }
    public required ulong GuildId { get; init; }
}
