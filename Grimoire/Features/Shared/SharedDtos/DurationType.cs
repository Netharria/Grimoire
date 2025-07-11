// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.SharedDtos;

public enum DurationType
{
    Minutes,
    Hours,
    Days
}

public static class DurationTypeExtensions
{
    public static DateTimeOffset GetDateTimeOffset(this DurationType durationType, long durationAmount)
        => durationType switch
        {
            DurationType.Minutes => DateTime.UtcNow.AddMinutes(durationAmount),
            DurationType.Hours => DateTime.UtcNow.AddHours(durationAmount),
            DurationType.Days => DateTime.UtcNow.AddDays(durationAmount),
            _ => throw new NotImplementedException()
        };

    public static TimeSpan GetTimeSpan(this DurationType durationType, long durationAmount)
        => durationType switch
        {
            DurationType.Minutes => TimeSpan.FromMinutes(durationAmount),
            DurationType.Hours => TimeSpan.FromHours(durationAmount),
            DurationType.Days => TimeSpan.FromDays(durationAmount),
            _ => throw new NotImplementedException()
        };
}
