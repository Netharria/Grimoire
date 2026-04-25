// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Globalization;

namespace Grimoire.Settings.Domain.Values;

public readonly record struct XpGainAmount
{
    private const int MaxValue = 100;
    private const int MinValue = 1;
    public static readonly XpGainAmount Default = new(5);

    private XpGainAmount(int value)
    {
        Value = value;
    }

    public int Value { get; }


    internal static XpGainAmount FromDatabaseOrDefault(string? input)
        => input is not null
            ? new XpGainAmount(int.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture))
            : Default;

    public static Validation<XpGainAmount> Create(string? inputStr)
    {
        if (!int.TryParse(inputStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var input)
            || input is not (>= MinValue and <= MaxValue))
            return Validation<XpGainAmount>.Fail(
                new Error("xp-gain-amount.invalid", "Xp gain amount must be a value between 1 and 100."));
        return Validation<XpGainAmount>.Succeed(new XpGainAmount(input));
    }

    public static Validation<XpGainAmount> Create(int? input)
    {
        if (input is not (>= MinValue and <= MaxValue))
            return Validation<XpGainAmount>.Fail(
                new Error("xp-gain-amount.invalid", "Xp gain amount must be a value between 1 and 100."));
        return Validation<XpGainAmount>.Succeed(new XpGainAmount(input.Value));
    }
}

public static class XpGainAmountExtensions
{
    extension(Validation<XpGainAmount> v)
    {
        internal Validation<string> ToDatabaseString()
            => v.Map(x => x.Value.ToString(CultureInfo.InvariantCulture));
    }
}
