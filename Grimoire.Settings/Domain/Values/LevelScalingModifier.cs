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

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    ///     Stored values are trusted, so a guild keeps its curve when its value is outside today's limits, as main
    ///     allowed (S-1). A missing value is the default, which the settings document decides, not this.
    /// </summary>
    internal static LevelScalingModifier FromDatabase(int value) => new(value);

    /// <summary>A new value, which must be within today's limits.</summary>
    public static Validation<Error, LevelScalingModifier> Create(int input)
        => input is >= MinValue and <= MaxValue
            ? Success<Error, LevelScalingModifier>(new LevelScalingModifier(input))
            : new Invalid("Level scaling modifier must be a value between 1 and 200.");
}
