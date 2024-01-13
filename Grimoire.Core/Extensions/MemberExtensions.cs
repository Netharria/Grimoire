// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Extensions;

public static class MemberExtensions
{
    public static int GetLevel(long xp, int @base, int modifier)
    {
        var i = 0;
        if (xp > 1000)
            i = (int)Math.Floor(Math.Sqrt((xp - @base) * 100 /
                (@base * modifier)));
        while (true)
        {
            var xpNeeded = @base + (
                (long)Math.Round(@base *
                                (modifier / 100.0) * i) * i);
            if (xp < xpNeeded)
                return i + 1;

            i += 1;
        }
    }
    public static long GetXpNeeded(int level, int @base, int modifier, int levelModifier = 0)
    {
        level = level - 2 + levelModifier;
        return level switch
        {
            < 0 => 0,
            0 => @base,
            _ => @base + ((long)Math.Round(@base * (modifier / 100.0) * level) * level)
        };
    }

    public static string Mention(this Member? member)
        => member switch
        {
            Member => $"<@!{member.UserId}>",
            null => "Unknown User",
        };
}
