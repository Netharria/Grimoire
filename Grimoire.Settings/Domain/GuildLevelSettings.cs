// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Shared;

namespace Grimoire.Settings.Domain;

public sealed class GuildLevelSettings : IModule
{
    public ulong GuildId { get; private init; }
    public TimeSpan TextTime { get; set; }
    public int Base { get; set; }
    public int Modifier { get; set; }
    public int Amount { get; set; }
    public ulong? LevelChannelLogId { get; set; }
    public GuildSettings? GuildSettings { get; set; }
    public bool ModuleEnabled { get; set; }

    public int GetLevelFromXp(long xp)
    {
        var i = 0;
        if (xp > 1000)
            // This is to reduce the number of iterations. Minor inaccuracy is acceptable.
            // ReSharper disable once PossibleLossOfFraction
            i = (int)Math.Floor(Math.Sqrt((xp - Base) * 100 /
                                          (Base * Modifier)));
        while (true)
        {
            var xpNeeded = Base + (
                (long)Math.Round(Base *
                                 (Modifier / 100.0) * i) * i);
            if (xp < xpNeeded)
                return i + 1;

            i += 1;
        }
    }

    public long GetXpNeededForLevel(int level, int levelModifier = 0)
    {
        level = level - 2 + levelModifier;
        return level switch
        {
            < 0 => 0,
            0 => Base,
            _ => Base + ((long)Math.Round(Base * (Modifier / 100.0) * level) * level)
        };
    }
}
