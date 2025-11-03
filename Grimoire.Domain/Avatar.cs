// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public sealed class Avatar
{
    public required AvatarFileName FileName { get; init; }
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    public required UserId UserId { get; init; }
    public required GuildId GuildId { get; init; }
}

public readonly record struct AvatarFileName(string Value)
{
    public override string ToString() => Value;
    [Pure]
    public static bool Equals(AvatarFileName? a, AvatarFileName? b)
        => a is { } aObj && b is { } bObj && string.Equals(aObj.Value, bObj.Value);

    [Pure]
    public static bool Equals(AvatarFileName? a, AvatarFileName? b, StringComparison stringComparison)
        => a is { } aObj && b is { } bObj && string.Equals(aObj.Value, bObj.Value, stringComparison);
}

