// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Discord.Enums;
using DSharpPlus.Entities;

namespace Cybermancy.Discord.Utilities
{
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