// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Globalization;

namespace Grimoire.Settings.Domain.Values;

public readonly record struct LevelScalingBase
{
    private const int MaxValue = 500;
    private const int MinValue = 1;
    public static readonly LevelScalingBase Default = new(15);

    private LevelScalingBase(int value)
    {
        Value = value;
    }

    public int Value { get; }

    public static Validation<LevelScalingBase> Create(string? inputStr)
    {
        if (!int.TryParse(inputStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var input)
            || input is not (>= MinValue and <= MaxValue))
            return Validation<LevelScalingBase>.Fail(
                new Error("level-scaling-base.invalid", "Level scaling base must be a value between 1 and 500."));
        return Validation<LevelScalingBase>.Succeed(new LevelScalingBase(input));
    }

    public static Validation<LevelScalingBase> Create(int? input)
    {
        if (input is not (>= MinValue and <= MaxValue))
            return Validation<LevelScalingBase>.Fail(
                new Error("level-scaling-base.invalid", "Level scaling base must be a value between 1 and 500."));
        return Validation<LevelScalingBase>.Succeed(new LevelScalingBase(input.Value));
    }
}

internal static class LevelingSettingsExtensions
{
    internal static Validation<string> ToDatabaseString(this Validation<LevelScalingBase> v)
        => v.Map(x => x.Value.ToString(CultureInfo.InvariantCulture));
}
