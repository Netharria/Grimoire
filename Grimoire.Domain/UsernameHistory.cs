// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public sealed record UsernameHistory
{
    public required UserId UserId { get; init; }
    public required Username Username { get; init; }
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}

public readonly record struct Username
{
    private Username(string value)
    {
        Value = value;
    }

    public string Value { get; }

    internal static Username FromDatabase(string value) => new(value);

    public static Validation<Username> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Validation<Username>.Fail(new Error("username.empty", "Username cannot be empty."));
        var trimmed = value.Trim();
        return trimmed.Length > 37
            ? Validation<Username>.Fail(new Error("username.too-long", "Username cannot exceed 37 characters."))
            : Validation<Username>.Succeed(new Username(trimmed));
    }

    [Pure]
    public bool Equals(Username other, StringComparison comparison)
        => string.Equals(Value, other.Value, comparison);

    public override string ToString() => Value;
}
