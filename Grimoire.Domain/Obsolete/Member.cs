// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain.Obsolete;

[UsedImplicitly]
[Obsolete("Table To be Dropped Soon.")]
public sealed class Member
{
    public ICollection<NicknameHistory> NicknamesHistory { get; init; } = [];
    public ICollection<Avatar> AvatarHistory { get; init; } = [];
    public ICollection<XpHistory> XpHistory { get; init; } = [];
    public required ulong GuildId { get; init; }

    public required ulong UserId { get; init; }
}
