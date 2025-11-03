// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public sealed class CustomCommand
{
    public required CustomCommandName Name { get; set; }
    public required GuildId GuildId { get; set; }
    public required string Content { get; set; }
    public required bool HasMention { get; set; }
    public required bool HasMessage { get; set; }
    public required bool IsEmbedded { get; set; }
    public CustomCommandEmbedColor? EmbedColor { get; set; }
    public required bool RestrictedUse { get; set; }

    public ICollection<CustomCommandRole> CustomCommandRoles { get; init; } = [];
}

public readonly record struct CustomCommandName(string Value)
{
    public override string ToString() => Value;
}


public readonly record struct CustomCommandEmbedColor(string Value)
{
    public override string ToString() => Value;
}
