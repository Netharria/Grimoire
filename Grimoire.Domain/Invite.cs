// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

public sealed record Invite
{
    public required InviteCode Code { get; init; }
    public required Username Inviter { get; init; }
    public required int Uses { get; init; }
    public required int MaxUses { get; init; }
    public required InviteUrl Url { get; init; }
}

public readonly record struct InviteCode
{
    private InviteCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    internal static InviteCode FromDatabase(string value) => new(value);

    public static Validation<InviteCode> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Validation<InviteCode>.Fail(new Error("invite-code.empty", "Invite code cannot be empty."));
        return Validation<InviteCode>.Succeed(new InviteCode(value.Trim()));
    }

    public override string ToString() => Value;
}

public readonly record struct InviteUrl
{
    private InviteUrl(string value)
    {
        Value = value;
    }

    public string Value { get; }

    internal static InviteUrl FromDatabase(string value) => new(value);

    public static Validation<InviteUrl> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Validation<InviteUrl>.Fail(new Error("invite-url.empty", "Invite URL cannot be empty."));
        return Validation<InviteUrl>.Succeed(new InviteUrl(value));
    }

    public override string ToString() => Value;
}
