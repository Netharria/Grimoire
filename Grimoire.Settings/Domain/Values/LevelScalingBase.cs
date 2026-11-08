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

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    ///     Stored values are trusted, so a guild keeps its curve when its value is outside today's limits, as main
    ///     allowed. With no value stored, or one that can't be read, the default applies.
    /// </summary>
    internal static LevelScalingBase FromDatabase(string? input)
        => int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? new(value) : Default;

    /// <summary>A new value, which must be within today's limits.</summary>
    public static Validation<LevelScalingBase> Create(int input)
        => input is >= MinValue and <= MaxValue
            ? Validation<LevelScalingBase>.Succeed(new LevelScalingBase(input))
            : Validation<LevelScalingBase>.Fail(new Error("level-scaling-base.invalid", "Level scaling base must be a value between 1 and 500."));
}
