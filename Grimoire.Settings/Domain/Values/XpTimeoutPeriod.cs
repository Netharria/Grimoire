// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Globalization;

namespace Grimoire.Settings.Domain.Values;

public readonly record struct XpTimeoutPeriod
{
    private const int MaxValueInt = 60;
    private const int MinValueInt = 1;
    private static readonly TimeSpan _maxValueTimeSpan = TimeSpan.FromMinutes(MaxValueInt);
    private static readonly TimeSpan _minValueTimeSpan = TimeSpan.FromMinutes(MinValueInt);
    public static readonly XpTimeoutPeriod Default = new(TimeSpan.FromMinutes(3));

    private XpTimeoutPeriod(TimeSpan value)
    {
        Value = value;
    }

    public TimeSpan Value { get; }

    internal static XpTimeoutPeriod FromDatabaseOrDefault(string? input)
        => input is not null
            ? new XpTimeoutPeriod(TimeSpan.Parse(input, CultureInfo.InvariantCulture))
            : Default;

    public static Validation<XpTimeoutPeriod> Create(int? input)
    {
        if (input is not (>= MinValueInt and <= MaxValueInt))
            return Validation<XpTimeoutPeriod>.Fail(
                new Error("xp-timeout-period.invalid",
                    "Xp timeout period must be a value between 1 and 60 in minutes."));
        return Validation<XpTimeoutPeriod>.Succeed(new XpTimeoutPeriod(TimeSpan.FromMinutes(input.Value)));
    }

    public static Validation<XpTimeoutPeriod> Create(string? inputStr)
    {
        if (string.IsNullOrWhiteSpace(inputStr)
            || !TimeSpan.TryParse(inputStr, CultureInfo.InvariantCulture, out var value)
            || value < _minValueTimeSpan
            || value > _maxValueTimeSpan)
            return Validation<XpTimeoutPeriod>.Fail(
                new Error("xp-timeout-period.invalid",
                    "Xp timeout period must be a value between 1 and 60 in minutes."));
        return Validation<XpTimeoutPeriod>.Succeed(new XpTimeoutPeriod(value));
    }
}

public static class XpTimeoutPeriodExtensions
{
    internal static Validation<string> ToDatabaseString(this Validation<XpTimeoutPeriod> v)
        => v.Map(x => x.Value.ToString("c", CultureInfo.InvariantCulture));
}
