// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public sealed record Avatar
{
    public required AvatarFileName FileName { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required UserId UserId { get; init; }
    public required GuildId GuildId { get; init; }
}

public readonly record struct AvatarFileName
{
    private AvatarFileName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    /// <summary>
    ///     Stored rows and Discord's own data are both trusted as they come, so there's nothing to validate.
    /// </summary>
    public static AvatarFileName Create(string value) => new(value);

    [Pure]
    public bool Equals(AvatarFileName other, StringComparison comparison)
        => string.Equals(Value, other.Value, comparison);

    public override string ToString() => Value;
}
