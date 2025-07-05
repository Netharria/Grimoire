// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;
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
public class XpHistory : IMember
{
    public virtual Member Member { get; init; } = null!;
    public virtual Guild Guild { get; init; } = null!;
    public long Xp { get; init; }
    public DateTimeOffset TimeOut { get; init; }
    public XpHistoryType Type { get; init; }
    public ulong? AwarderId { get; init; }
    public Member? Awarder { get; init; }
    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }
}
