// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public sealed record ProxiedMessageLink
{
    public required MessageId ProxyMessageId { get; init; }
    public Message? ProxyMessage { get; init; }
    public required MessageId OriginalMessageId { get; init; }
    public Message? OriginalMessage { get; init; }
    public required PluralKitSystemId SystemId { get; init; }
    public required PluralKitMemberId MemberId { get; init; }
}

public readonly record struct PluralKitSystemId
{
    private PluralKitSystemId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    internal static PluralKitSystemId FromDatabase(string value) => new(value);

    public static Validation<PluralKitSystemId> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Validation<PluralKitSystemId>.Fail(new Error("pluralkit-system-id.empty",
                "PluralKit system ID cannot be empty."));
        return Validation<PluralKitSystemId>.Succeed(new PluralKitSystemId(value.Trim()));
    }

    public override string ToString() => Value;
}

public readonly record struct PluralKitMemberId
{
    private PluralKitMemberId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    internal static PluralKitMemberId FromDatabase(string value) => new(value);

    public static Validation<PluralKitMemberId> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Validation<PluralKitMemberId>.Fail(new Error("pluralkit-member-id.empty",
                "PluralKit member ID cannot be empty."));
        return Validation<PluralKitMemberId>.Succeed(new PluralKitMemberId(value.Trim()));
    }

    public override string ToString() => Value;
}
