// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public class CustomCommand
{
    public string Name { get; set; } = string.Empty;
    public ulong GuildId { get; set; }
    public virtual Guild Guild { get; init; } = null!;
    public string Content { get; set; } = string.Empty;
    public bool HasMention { get; set; }
    public bool HasMessage { get; set; }
    public bool IsEmbedded { get; set; }
    public string? EmbedColor { get; set; }

    public bool RestrictedUse { get; set; }

    public ICollection<CustomCommandRole> CustomCommandRoles { get; init; } = [];
}
