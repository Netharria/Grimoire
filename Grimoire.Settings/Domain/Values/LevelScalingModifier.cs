// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Globalization;

namespace Grimoire.Settings.Domain.Values;

public readonly record struct LevelScalingModifier
{
    private const int MaxValue = 200;
    private const int MinValue = 1;
    public static readonly LevelScalingModifier Default = new(50);

    private LevelScalingModifier(int value)
    {
        Value = value;
    }

    public int Value { get; }

    public static Validation<LevelScalingModifier> Create(string? inputStr)
    {
        if (!int.TryParse(inputStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var input)
            || input is not (>= MinValue and <= MaxValue))
            return Validation<LevelScalingModifier>.Fail(
                new Error("level-scaling-modifier.invalid",
                    "Level scaling modifier must be a value between 1 and 200."));
        return Validation<LevelScalingModifier>.Succeed(new LevelScalingModifier(input));
    }

    public static Validation<LevelScalingModifier> Create(int? input)
    {
        if (input is not (>= MinValue and <= MaxValue))
            return Validation<LevelScalingModifier>.Fail(
                new Error("level-scaling-modifier.invalid",
                    "Level scaling modifier must be a value between 1 and 200."));
        return Validation<LevelScalingModifier>.Succeed(new LevelScalingModifier(input.Value));
    }
}

public static class LevelScalingModifierExtensions
{
    internal static Validation<string> ToDatabaseString(this Validation<LevelScalingModifier> v)
        => v.Map(x => x.Value.ToString(CultureInfo.InvariantCulture));
}
