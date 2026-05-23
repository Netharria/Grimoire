// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public sealed record NicknameHistory
{
    public required Nickname? Nickname { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required UserId UserId { get; init; }
    public required GuildId GuildId { get; init; }
}

public readonly record struct Nickname
{
    private Nickname(string value)
    {
        Value = value;
    }

    public string Value { get; }

    internal static Nickname FromDatabase(string value) => new(value);

    public static Validation<Nickname> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Validation<Nickname>.Fail(new Error("nickname.empty", "Nickname cannot be empty."));
        var trimmed = value.Trim();
        return trimmed.Length > 32
            ? Validation<Nickname>.Fail(new Error("nickname.too-long", "Nickname cannot exceed 32 characters."))
            : Validation<Nickname>.Succeed(new Nickname(trimmed));
    }

    public static Nickname? CreateIfNotEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new Nickname(value.Trim());

    [Pure]
    public bool Equals(Nickname other, StringComparison comparison)
        => string.Equals(Value, other.Value, comparison);

    public override string ToString() => Value;
}
