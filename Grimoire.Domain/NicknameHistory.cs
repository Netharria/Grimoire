// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public sealed class NicknameHistory
{
    public Member? Member { get; init; }
    public required string? Nickname { get; init; }
    public DateTimeOffset Timestamp { get; private init; }
    public Guild? Guild { get; init; }
    public required ulong UserId { get; init; }
    public required ulong GuildId { get; init; }
}
