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
        => duration switch
        {
            Duration.Days => TimeSpan.FromDays(timeValue),
            Duration.Months => TimeSpan.FromDays(timeValue * 30),
            Duration.Years => TimeSpan.FromDays(timeValue * 365),
            _ => throw new NotSupportedException()
        };
}
