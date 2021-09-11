using System;
using Cybermancy.Core.Enums;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Utilities
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
                _ => throw new ArgumentOutOfRangeException(nameof(color), color, null)
            };
        }
        
    }
}