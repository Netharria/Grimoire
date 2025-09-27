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
public sealed class User
{
    public ICollection<UsernameHistory> UsernameHistories { get; init; } = [];
    public required ulong Id { get; init; }
}
