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
        public static uint GetLevel(this Member member)
            => GetLevel(member.Xp,
                member.Guild.LevelSettings.Base,
                member.Guild.LevelSettings.Modifier);

        public static uint GetLevel(this Member member, uint @base, uint modifier)
            => GetLevel(member.Xp, @base, modifier);

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
        public static ulong GetXpNeeded(this Member member)
            => GetXpNeeded(
                member.Xp,
                member.Guild.LevelSettings.Base,
                member.Guild.LevelSettings.Modifier,
                0);

        public static ulong GetXpNeeded(this Member member, uint levelModifier)
            => GetXpNeeded(
                member.Xp,
                member.Guild.LevelSettings.Base,
                member.Guild.LevelSettings.Modifier,
                levelModifier);

        public static ulong GetXpNeeded(this Member member, uint @base, uint modifier)
            => GetXpNeeded(member.Xp, @base, modifier, 0);

        public static ulong GetXpNeeded(this Member member, uint @base, uint modifier, uint levelModifier)
            => GetXpNeeded(member.Xp, @base, modifier, levelModifier);

        public static ulong GetXpNeeded(ulong xp, uint @base, uint modifier)
            => GetXpNeeded(xp, @base, modifier, 0);

        public static ulong GetXpNeeded(ulong xp, uint @base, uint modifier, uint levelModifier)
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

        public static void GrantXp(this Member member, uint amount, TimeSpan textTime, uint? xpAmount = null)
        {
            xpAmount ??= amount;
            member.Xp += xpAmount.Value;
            member.TimeOut = DateTime.UtcNow + textTime;
        }
    }
}
