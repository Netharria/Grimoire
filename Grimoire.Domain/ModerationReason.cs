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

    internal static ModerationReason FromDatabase(string value) => new(value);

    public static Validation<Error, Option<ModerationReason>> CreateIfNotNull(string? input)
        => input is null
            ? Option<ModerationReason>.None
            : Create(input).Map(Optional);

    /// <summary>For an optional reason: none given, or only whitespace, means no reason.</summary>
    public static Validation<Error, Option<ModerationReason>> CreateIfNotBlank(string? input)
        => string.IsNullOrWhiteSpace(input)
            ? Option<ModerationReason>.None
            : Create(input).Map(Optional);

    public static Validation<Error, ModerationReason> Create(string input)
    {
        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 1000)
            return new Invalid("Reason must be 1\u20131000 non-whitespace characters.");
        return Success<Error, ModerationReason>(new ModerationReason(trimmed));
    }

    public override string ToString() => Value;
}
