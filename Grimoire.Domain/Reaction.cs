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
public class Reaction : IMember
{
    public ulong MessageId { get; init; }

    public virtual Message Message { get; init; } = null!;

    public virtual Member Member { get; init; } = null!;

    public virtual Guild Guild { get; init; } = null!;

    public ulong EmojiId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ImageUrl { get; init; } = string.Empty;

    public ulong UserId { get; set; }

    public ulong GuildId { get; set; }
}
