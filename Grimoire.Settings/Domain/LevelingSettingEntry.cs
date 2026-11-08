// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Values;

namespace Grimoire.Settings.Domain;

public sealed record LevelingSettingEntry(
    XpTimeoutPeriod XpTimeoutPeriod,
    LevelScalingModifier Modifier,
    LevelScalingBase Base,
    XpGainAmount Amount)
{
    /// <summary>
    ///     The level for this much XP: one more than the number of thresholds it has reached. The thresholds never
    ///     go down, so a binary search finds that count in about 70 steps for any XP, however absurd. Counting up one
    ///     level at a time would take billions of steps for a huge award and freeze the bot; an estimate to skip
    ///     ahead could land past the answer for small Base and Modifier.
    /// </summary>
    public int GetLevelFromXp(long xp)
    {
        // Find the first step whose threshold is above the XP. Widen the range until it holds one, then halve it.
        long high = 1;
        while (!this.IsAbove(high, xp))
        {
            if (high >= int.MaxValue)
                return int.MaxValue;
            high *= 2;
        }

        long low = 0;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (this.IsAbove(middle, xp))
                high = middle;
            else
                low = middle + 1;
        }

        return (int)Math.Min(low + 1, int.MaxValue);
    }

    /// <summary>
    ///     Whether the XP threshold at <paramref name="step" /> is above <paramref name="xp" />. Worked out in 128 bits,
    ///     since at the extremes the threshold doesn't fit in a long.
    /// </summary>
    private bool IsAbove(long step, long xp)
        => Base.Value + (Int128)(long)Math.Round(Base.Value * (Modifier.Value / 100.0) * step) * step > xp;

    public long GetXpNeededForLevel(int level, int levelModifier = 0)
    {
        level = level - 2 + levelModifier;
        return level switch
        {
            < 0 => 0,
            0 => Base.Value,
            _ => Base.Value + ((long)Math.Round(Base.Value * (Modifier.Value / 100.0) * level) * level)
        };
    }
}
