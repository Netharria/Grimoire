using System;
using Cybermancy.Domain;

namespace Cybermancy.Core.Extensions
{
    public static class RewardExtension
    {
        public static int GetXpNeeded(this Reward reward)
        {
            var level = reward.RewardLevel - 2;
            return level switch
            {
                0 => reward.Guild.LevelSettings.Base,
                < 0 => 0,
                _ => reward.Guild.LevelSettings.Base + (int) Math.Round(reward.Guild.LevelSettings.Base *
                                                                         (reward.Guild.LevelSettings.Modifier / 100.0) *
                                                                         level) * level
            };
        }
    }
}