// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public sealed record CustomCommand
{
    public required CustomCommandName Name { get; init; }
    public required GuildId GuildId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string Content { get; init; }
    public required bool HasMention { get; init; }
    public required bool HasMessage { get; init; }
    public required bool IsEmbedded { get; init; }
    public CustomCommandEmbedColor? EmbedColor { get; init; }
    public required bool RestrictedUse { get; init; }
    public ModeratorId? ModeratorId { get; init; }

    public ICollection<CustomCommandRole> Roles { get; init; } = [];
}

public readonly record struct CustomCommandName(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct CustomCommandEmbedColor(string Value)
{
    public override string ToString() => Value;
}
