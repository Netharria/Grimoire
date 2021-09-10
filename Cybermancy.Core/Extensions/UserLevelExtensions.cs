using System;
using Cybermancy.Domain;

namespace Cybermancy.Core.Extensions
{
    public static class UserLevelExtensions
    {
        public static int GetLevel(this UserLevels userLevels)
        {
            var i = 0;
            while (true)
            {
                var xpNeeded = userLevels.Guild.LevelSettings.Base + (
                    (int)Math.Round(userLevels.Guild.LevelSettings.Base * 
                                    (userLevels.Guild.LevelSettings.Modifier / 100.0) * i) * i
                );
                if (userLevels.Xp < xpNeeded)
                {
                    return i + 1;
                }

                i += 1;
            }
        }

        public static int GetXpNeeded(this UserLevels userLevels, int levelModifier = 0)
        {
            var level = userLevels.GetLevel() - 2 + levelModifier;
            return level switch
            {
                0 => userLevels.Guild.LevelSettings.Base,
                < 0 => 0,
                _ => userLevels.Guild.LevelSettings.Base + ((int) Math.Round(userLevels.Guild.LevelSettings.Base *
                                                                             (userLevels.Guild.LevelSettings.Modifier /
                                                                              100.0) * level) * level)
            };
        }

        public static void GrantXp(this UserLevels userLevels, int? xpAmount = null)
        {
            xpAmount ??= userLevels.Guild.LevelSettings.Amount;
            userLevels.Xp += xpAmount.Value;
            userLevels.TimeOut = userLevels.Guild.LevelSettings.GetTextTimeout();
        }
    }
}