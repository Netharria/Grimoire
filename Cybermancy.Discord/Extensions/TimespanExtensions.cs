// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Cybermancy.Discord.Extensions
{
    public static class TimespanExtensions
    {
        public static string CustomTimeSpanString(this TimeSpan timeSpan)
        {
            var stringBuilder = new StringBuilder();
            if (1 < timeSpan.Days) stringBuilder.Append(timeSpan.Days).Append(" days ");
            if (1 == timeSpan.Days) stringBuilder.Append(timeSpan.Days).Append(" day ");
            if (1 < timeSpan.Hours) stringBuilder.Append(timeSpan.Hours).Append(" hours ");
            if (1 == timeSpan.Hours) stringBuilder.Append(timeSpan.Hours).Append(" hour ");
            if (1 < timeSpan.Minutes) stringBuilder.Append(timeSpan.Hours).Append(" minutes ");
            if (1 == timeSpan.Minutes) stringBuilder.Append(timeSpan.Hours).Append(" minute ");
            if (0 == timeSpan.Days && 1 < timeSpan.Seconds) stringBuilder.Append(timeSpan.Hours).Append(" seconds ");
            if (0 == timeSpan.Days && 1 == timeSpan.Seconds) stringBuilder.Append(timeSpan.Hours).Append(" second ");
            return stringBuilder.ToString();
        }
    }
}
