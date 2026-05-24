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

    public static Validation<RewardMessage?> CreateIfNotNull(string? input)
        => input is null
            ? Validation<RewardMessage?>.Succeed(null)
            : Create(input).Map(r => (RewardMessage?)r);

    public static Validation<RewardMessage> Create(string? input)
    {
        var trimmed = input?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 4096)
            return Validation<RewardMessage>.Fail(
                new Error("reward-message.invalid", "Reward message must be 1\u20134096 non-whitespace characters."));
        return Validation<RewardMessage>.Succeed(new RewardMessage(trimmed));
    }

    public override string ToString() => Value;
}
