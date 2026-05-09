// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

public readonly record struct ModerationReason
{
    private ModerationReason(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ModerationReason FromDatabase(string value) => new(value);

    public static Validation<ModerationReason?> CreateIfNotNull(string? input)
        => input switch
        {
            not null => Create(input).Map(reason => (ModerationReason?)reason),
            _ => Validation<ModerationReason?>.Succeed(null)
        };

    public static Validation<ModerationReason> Create(string input)
    {
        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 1000)
            return Validation<ModerationReason>.Fail(
                new Error("moderation-reason.invalid", "Reason must be 1\u20131000 non-whitespace characters."));
        return Validation<ModerationReason>.Succeed(new ModerationReason(trimmed));
    }

    public override string ToString() => Value;
}
