// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Domain;

namespace Cybermancy.Core.Extensions
{
    public static class GuildUserExtensions
    {
        public static int GetLevel(this GuildUser guildUser, ICybermancyDbContext cybermancyDbContext)
        {
            var levelSettings = cybermancyDbContext.GuildLevelSettings
                .Where(x => x.GuildId == guildUser.GuildId)
                .Select(x => new { x.Base, x.Modifier })
                .First();
            return GetLevel(guildUser, levelSettings.Base, levelSettings.Modifier);
        }

        public static int GetLevel(this GuildUser guildUser, int @base, int modifier)
        {
            var i = 0;
            if (guildUser.Xp > 1000)
                i = (int)Math.Floor(Math.Sqrt((guildUser.Xp - @base) * 100 /
                    (@base * modifier)));
            while (true)
            {
                var xpNeeded = @base + (
                    (int)Math.Round(@base *
                                    (modifier / 100.0) * i) * i);
                if (guildUser.Xp < xpNeeded)
                    return i + 1;

                i += 1;
            }
        }

        public static int GetXpNeeded(this GuildUser guildUser, ICybermancyDbContext cybermancyDbContext, int levelModifier = 0)
        {
            var levelSettings = cybermancyDbContext.GuildLevelSettings
                .Where(x => x.GuildId == guildUser.GuildId)
                .Select(x => new { x.Base, x.Modifier })
                .First();
            return GetXpNeeded(guildUser, levelSettings.Base, levelSettings.Modifier, levelModifier);
        }

        public static int GetXpNeeded(this GuildUser guildUser, int @base, int modifier, int levelModifier = 0)
        {
            
            var level = guildUser.GetLevel(@base, modifier) - 2 + levelModifier;
            return level switch
            {
                0 => @base,
                < 0 => 0,
                _ => @base + ((int)Math.Round(@base * (modifier / 100.0) * level) * level)
            };
        }

        public static void GrantXp(this GuildUser guildUser, ICybermancyDbContext cybermancyDbContext, int? xpAmount = null)
        {
            var levelSettings = cybermancyDbContext.GuildLevelSettings
                .Where(x => x.GuildId == guildUser.GuildId)
                .Select(x => new { x.Amount, x.TextTime })
                .First();
            guildUser.GrantXp(levelSettings.Amount, levelSettings.TextTime, xpAmount);
        }

        public static void GrantXp(this GuildUser guildUser, int amount, int textTime, int? xpAmount = null)
        {
            xpAmount ??= amount;
            guildUser.Xp += xpAmount.Value;
            guildUser.TimeOut = DateTime.UtcNow + TimeSpan.FromMinutes(textTime);
        }
    }
}
