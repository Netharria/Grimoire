// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;

namespace Grimoire.Enums;

public enum Duration
{
    [ChoiceDisplayName("Days")] Days,
    [ChoiceDisplayName("Months")] Months,
    [ChoiceDisplayName("Years")] Years
}

public static class DurationExtensions
{
    public static TimeSpan GetTimeSpan(this Duration duration, long timeValue)
        => TimeSpan.FromDays(duration.GetDays(timeValue));

    public static long GetDays(this Duration duration, long timeValue)
        => duration switch
        {
            Duration.Days => timeValue,
            Duration.Months => timeValue * 30,
            Duration.Years => timeValue * 365,
            _ => throw new NotSupportedException()
        };
}
