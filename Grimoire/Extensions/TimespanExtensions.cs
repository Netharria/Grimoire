// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Grimoire.Extensions;

public static class TimespanExtensions
{
    public static string CustomTimeSpanString(this TimeSpan timeSpan)
    {
        var stringBuilder = new StringBuilder();
        switch (timeSpan.Days)
        {
            case > 1:
                stringBuilder.Append(timeSpan.Days).Append(" days ");
                break;
            case 1:
                stringBuilder.Append(timeSpan.Days).Append(" day ");
                break;
        }

        switch (timeSpan.Hours)
        {
            case > 1:
                stringBuilder.Append(timeSpan.Hours).Append(" hours ");
                break;
            case 1:
                stringBuilder.Append(timeSpan.Hours).Append(" hour ");
                break;
        }

        switch (timeSpan.Minutes)
        {
            case > 1:
                stringBuilder.Append(timeSpan.Minutes).Append(" minutes ");
                break;
            case 1:
                stringBuilder.Append(timeSpan.Minutes).Append(" minute ");
                break;
        }

        if (timeSpan is { Days: 0, Seconds: > 1 }) stringBuilder.Append(timeSpan.Seconds).Append(" seconds ");
        if (timeSpan is { Days: 0, Seconds: 1 }) stringBuilder.Append(timeSpan.Seconds).Append(" second ");
        return stringBuilder.ToString();
    }
}
