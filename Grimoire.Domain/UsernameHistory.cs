// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public class UsernameHistory
{
    public ulong UserId { get; init; }
    public virtual User User { get; init; } = null!;
    public string Username { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
}
