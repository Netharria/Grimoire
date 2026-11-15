// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain.Values;

public readonly record struct RewardMessage
{
    private RewardMessage(string value)
    {
        Value = value;
    }

    public string Value { get; }

    internal static RewardMessage? FromDatabase(string? value) =>
        value is not null
            ? new RewardMessage(value)
            : null;

    public static Validation<Error, Option<RewardMessage>> CreateIfNotNull(string? input)
        => input is null
            ? Option<RewardMessage>.None
            : Create(input).Map(Optional);

    public static Validation<Error, RewardMessage> Create(string? input)
    {
        var trimmed = input?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 4096)
            return new Invalid("Reward message must be 1\u20134096 non-whitespace characters.");
        return Success<Error, RewardMessage>(new RewardMessage(trimmed));
    }

    public override string ToString() => Value;
}
