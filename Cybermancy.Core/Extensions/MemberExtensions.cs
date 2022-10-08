// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain;

namespace Cybermancy.Core.Extensions
{
    public static class MemberExtensions
    {
        public static int GetLevel(this Member member)
            => GetLevel(member.XpHistory.Sum(x => x.Xp),
                member.Guild.LevelSettings.Base,
                member.Guild.LevelSettings.Modifier);

        public static int GetLevel(this Member member, int @base, int modifier)
            => GetLevel(member.XpHistory.Sum(x => x.Xp), @base, modifier);

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
        public static long GetXpNeeded(this Member member)
            => GetXpNeeded(
                member.XpHistory.Sum(x => x.Xp),
                member.Guild.LevelSettings.Base,
                member.Guild.LevelSettings.Modifier,
                0);

        public static long GetXpNeeded(this Member member, int levelModifier)
            => GetXpNeeded(
                member.XpHistory.Sum(x => x.Xp),
                member.Guild.LevelSettings.Base,
                member.Guild.LevelSettings.Modifier,
                levelModifier);

        public static long GetXpNeeded(this Member member, int @base, int modifier)
            => GetXpNeeded(member.XpHistory.Sum(x => x.Xp), @base, modifier, 0);

        public static long GetXpNeeded(this Member member, int @base, int modifier, int levelModifier)
            => GetXpNeeded(member.XpHistory.Sum(x => x.Xp), @base, modifier, levelModifier);

        public static long GetXpNeeded(long xp, int @base, int modifier)
            => GetXpNeeded(xp, @base, modifier, 0);

        public static long GetXpNeeded(long xp, int @base, int modifier, int levelModifier)
        {
            var currentLevel = GetLevel(xp, @base, modifier);

            if (currentLevel - 2 + levelModifier < 0)
                return 0;

            var level = currentLevel - 2 + levelModifier;
            return level switch
            {
                0 => @base,
                _ => @base + ((long)Math.Round(@base * (modifier / 100.0) * level) * level)
            };
        }
    }
}
