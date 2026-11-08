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
    public required Username? Inviter { get; init; }
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

    /// <summary>
    ///     Stored rows and Discord's own data are both trusted as they come, so there's nothing to validate.
    /// </summary>
    public static InviteCode Create(string value) => new(value);

    public override string ToString() => Value;
}

public readonly record struct InviteUrl
{
    private InviteUrl(string value)
    {
        Value = value;
    }

    public string Value { get; }

    /// <summary>
    ///     Stored rows and Discord's own data are both trusted as they come, so there's nothing to validate.
    /// </summary>
    public static InviteUrl Create(string value) => new(value);

    public override string ToString() => Value;
}
