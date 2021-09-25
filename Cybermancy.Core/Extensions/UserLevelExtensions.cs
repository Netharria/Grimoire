// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using Cybermancy.Domain;

namespace Cybermancy.Core.Extensions
{
    public static class UserLevelExtensions
    {
        public static int GetLevel(this UserLevel userLevel)
        {
            var i = 0;
            while (true)
            {
                var xpNeeded = userLevel.Guild.LevelSettings.Base + (
                    (int)Math.Round(userLevel.Guild.LevelSettings.Base *
                                    (userLevel.Guild.LevelSettings.Modifier / 100.0) * i) * i);
                if (userLevel.Xp < xpNeeded)
                {
                    return i + 1;
                }

                i += 1;
            }
        }

        public static int GetXpNeeded(this UserLevel userLevel, int levelModifier = 0)
        {
            var level = userLevel.GetLevel() - 2 + levelModifier;
            return level switch
            {
                0 => userLevel.Guild.LevelSettings.Base,
                < 0 => 0,
                _ => userLevel.Guild.LevelSettings.Base + ((int)Math.Round(userLevel.Guild.LevelSettings.Base *
                                                                             (userLevel.Guild.LevelSettings.Modifier /
                                                                              100.0) * level) * level)
            };
        }

        public static void GrantXp(this UserLevel userLevel, int? xpAmount = null)
        {
            xpAmount ??= userLevel.Guild.LevelSettings.Amount;
            userLevel.Xp += xpAmount.Value;
            userLevel.TimeOut = userLevel.Guild.LevelSettings.GetTextTimeout();
        }
    }
}