// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;
using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public class Role : IIdentifiable<ulong>
{
    public ulong GuildId { get; init; }

    public virtual Guild Guild { get; init; } = null!;

    public virtual Reward? Reward { get; init; }

    public virtual IgnoredRole? IsIgnoredRole { get; init; }
    public virtual ICollection<CustomCommandRole> CustomCommandRoles { get; init; } = [];
    public ulong Id { get; set; }
}
