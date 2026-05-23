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

    internal static AvatarFileName FromDatabase(string value) => new(value);

    public static Validation<AvatarFileName> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Validation<AvatarFileName>.Fail(new Error("avatar-file-name.empty", "Avatar URL cannot be empty."));
        return Validation<AvatarFileName>.Succeed(new AvatarFileName(value));
    }

    public static AvatarFileName? CreateIfNotEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new AvatarFileName(value);

    [Pure]
    public bool Equals(AvatarFileName other, StringComparison comparison)
        => string.Equals(Value, other.Value, comparison);

    public override string ToString() => Value;
}
