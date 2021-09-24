// -----------------------------------------------------------------------
// <copyright file="ColorUtility.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Core.Utilities
{
    using System;
    using Cybermancy.Core.Enums;
    using DSharpPlus.Entities;

    public static class ColorUtility
    {
        public static DiscordColor GetColor(CybermancyColor color)
        {
            return color switch
            {
                CybermancyColor.Purple => new DiscordColor(108, 0, 209),
                CybermancyColor.Orange => new DiscordColor(252, 119, 3),
                CybermancyColor.Green => new DiscordColor(3, 252, 111),
                CybermancyColor.Gold => new DiscordColor(252, 194, 3),
                _ => throw new ArgumentOutOfRangeException(nameof(color), color, message: null)
            };
        }

    }
}