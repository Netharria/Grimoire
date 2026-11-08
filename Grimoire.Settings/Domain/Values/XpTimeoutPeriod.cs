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
    private const int MaxMinutes = 60;
    private const int MinMinutes = 1;
    public static readonly XpTimeoutPeriod Default = new(TimeSpan.FromMinutes(3));

    private XpTimeoutPeriod(TimeSpan value)
    {
        Value = value;
    }

    public TimeSpan Value { get; }

    /// <summary>The stored form, hours:minutes:seconds.</summary>
    public override string ToString() => Value.ToString("c", CultureInfo.InvariantCulture);

    /// <summary>
    ///     Stored values are trusted, so a guild keeps its timeout when it's outside today's limits, as main allowed.
    ///     The migration writes total hours, so a timeout of a day or more reads as "25:00:00", which TimeSpan won't
    ///     parse. With no value stored, or one that can't be read, the default applies.
    /// </summary>
    internal static XpTimeoutPeriod FromDatabase(string? input)
        => input?.Split(':') is [var hours, var minutes, var seconds]
           && long.TryParse(hours, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h)
           && int.TryParse(minutes, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m)
           && int.TryParse(seconds, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)
            ? new(TimeSpan.FromHours(h) + TimeSpan.FromMinutes(m) + TimeSpan.FromSeconds(s))
            : Default;

    /// <summary>A new timeout in minutes, which must be within today's limits.</summary>
    public static Validation<XpTimeoutPeriod> Create(int minutes)
        => minutes is >= MinMinutes and <= MaxMinutes
            ? Validation<XpTimeoutPeriod>.Succeed(new XpTimeoutPeriod(TimeSpan.FromMinutes(minutes)))
            : Validation<XpTimeoutPeriod>.Fail(new Error("xp-timeout-period.invalid",
                "Xp timeout period must be a value between 1 and 60 in minutes."));
}
