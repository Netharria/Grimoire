// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;

namespace Grimoire.Domain;

public enum XpHistoryType
{
    Earned,
    Awarded,
    Reclaimed,
    Migrated,
    Created
}
public class XpHistory : IMember
{
    public long Id { get; set; }
    public ulong UserId { get; set; }
    public virtual Member Member { get; set; } = null!;
    public ulong GuildId { get; set; }
    public virtual Guild Guild { get; set; } = null!;
    public long Xp { get; set; }
    public DateTimeOffset TimeOut { get; set; }
    public XpHistoryType Type { get; set; }
    public ulong? AwarderId { get; set; }
    public Member? Awarder { get; set; }
}
