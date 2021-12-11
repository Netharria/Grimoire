// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Reflection;
using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cybermancy.Core.Extensions
{
    public static class GuildUserExtensions
    {
        public static ICybermancyDbContext CybermancyDbContext { get; set; }
        public static int GetLevel(this GuildUser guildUser)
        {
            var levelSettings = CybermancyDbContext.GuildLevelSettings
                .Where(x => x.GuildId == guildUser.GuildId)
                .Select(x => new { x.Base, x.Modifier })
                .First();
            var i = 0;
            while (true)
            {
                var xpNeeded = levelSettings.Base + (
                    (int)Math.Round(levelSettings.Base *
                                    (levelSettings.Modifier / 100.0) * i) * i);
                if (guildUser.Xp < xpNeeded)
                {
                    return i + 1;
                }

                i += 1;
            }
        }

        public static int GetXpNeeded(this GuildUser guildUser, int levelModifier = 0)
        {
            var levelSettings = CybermancyDbContext.GuildLevelSettings
                .Where(x => x.GuildId == guildUser.GuildId)
                .Select(x => new { x.Base, x.Modifier })
                .First();
            var level = guildUser.GetLevel() - 2 + levelModifier;
            return level switch
            {
                0 => levelSettings.Base,
                < 0 => 0,
                _ => levelSettings.Base + ((int)Math.Round(levelSettings.Base *
                                                (levelSettings.Modifier /
                                                100.0) * level) * level)
            };
        }

        public static void GrantXp(this GuildUser guildUser, int? xpAmount = null)
        {
            var levelSettings = CybermancyDbContext.GuildLevelSettings
                .Where(x => x.GuildId == guildUser.GuildId)
                .Select(x => new { x.Amount, x.TextTime })
                .First();
            xpAmount ??= levelSettings.Amount;
            guildUser.Xp += xpAmount.Value;
            guildUser.TimeOut = DateTime.UtcNow + TimeSpan.FromMinutes(levelSettings.TextTime);
        }
    }
}
