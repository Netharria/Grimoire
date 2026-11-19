// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

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

    /// <summary>
    ///     Stored values are trusted, so a guild keeps its timeout when it's outside today's limits, as main allowed
    ///     (S-1). Stored as whole seconds (spec 2.2).
    /// </summary>
    internal static XpTimeoutPeriod FromDatabase(long seconds) => new(TimeSpan.FromSeconds(seconds));

    internal long TotalSeconds => (long)Value.TotalSeconds;

    /// <summary>A new timeout in minutes, which must be within today's limits.</summary>
    public static Validation<Error, XpTimeoutPeriod> Create(int minutes)
        => minutes is >= MinMinutes and <= MaxMinutes
            ? Success<Error, XpTimeoutPeriod>(new XpTimeoutPeriod(TimeSpan.FromMinutes(minutes)))
            : new Invalid("Xp timeout period must be a value between 1 and 60 in minutes.");
}
