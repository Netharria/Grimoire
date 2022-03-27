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
        public static uint GetLevel(this GuildUser guildUser, ICybermancyDbContext cybermancyDbContext)
        {
            var levelSettings = cybermancyDbContext.GuildLevelSettings
                .Where(x => x.GuildId == guildUser.GuildId)
                .Select(x => new { x.Base, x.Modifier })
                .First();
            return GetLevel(guildUser, levelSettings.Base, levelSettings.Modifier);
        }

        public static uint GetLevel(this GuildUser guildUser, uint @base, uint modifier)
            => GetLevel(guildUser.Xp, @base, modifier);

        public static uint GetLevel(ulong xp, uint @base, uint modifier)
        {
            var i = 0;
            if (xp > 1000)
                i = (int)Math.Floor(Math.Sqrt((xp - @base) * 100 /
                    (@base * modifier)));
            while (true)
            {
                var xpNeeded = (ulong)(@base + (
                    (int)Math.Round(@base *
                                    (modifier / 100.0) * i) * i));
                if (xp < xpNeeded)
                    return (uint)i + 1;

                i += 1;
            }
        }

        public static ulong GetXpNeeded(this GuildUser guildUser, ICybermancyDbContext cybermancyDbContext, uint levelModifier = 0)
        {
            var levelSettings = cybermancyDbContext.GuildLevelSettings
                .Where(x => x.GuildId == guildUser.GuildId)
                .Select(x => new { x.Base, x.Modifier })
                .First();
            return GetXpNeeded(guildUser, levelSettings.Base, levelSettings.Modifier, levelModifier);
        }

        public static ulong GetXpNeeded(this GuildUser guildUser, uint @base, uint modifier, uint levelModifier = 0)
            => GetXpNeeded(guildUser.Xp, @base, modifier, levelModifier);

        public static ulong GetXpNeeded(ulong xp, uint @base, uint modifier, uint levelModifier = 0)
        {
            var currentLevel = GetLevel(xp, @base, modifier);

            if ((int)currentLevel - 2 + (int)levelModifier < 0)
                return 0;

            var level = currentLevel - 2 + levelModifier;
            return level switch
            {
                0 => @base,
                _ => @base + ((uint)Math.Round(@base * (modifier / 100.0) * (ulong)level) * (ulong)level)
            };
        }

        public static void GrantXp(this GuildUser guildUser, ICybermancyDbContext cybermancyDbContext, uint? xpAmount = null)
        {
            var levelSettings = cybermancyDbContext.GuildLevelSettings
                .Where(x => x.GuildId == guildUser.GuildId)
                .Select(x => new { x.Amount, x.TextTime })
                .First();
            guildUser.GrantXp(levelSettings.Amount, levelSettings.TextTime, xpAmount);
        }

        public static void GrantXp(this GuildUser guildUser, uint amount,uint textTime, uint? xpAmount = null)
        {
            xpAmount ??= amount;
            guildUser.Xp += xpAmount.Value;
            guildUser.TimeOut = DateTime.UtcNow + TimeSpan.FromMinutes(textTime);
        }
    }
}
