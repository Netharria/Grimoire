// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using DSharpPlus.SlashCommands;

namespace Cybermancy.Utilities
{
    public static class DiscordSnowflakeParser
    {
        public static Dictionary<string, string[]> ParseStringIntoIdsAndGroupByType(InteractionContext ctx, string value) =>
            Regex.Matches(value, @"(\d{17,21})", RegexOptions.None, TimeSpan.FromSeconds(1))
                .Where(x => x.Success)
                .Select(x => x.Value)
                .GroupBy(async x =>
                {
                    if (!ulong.TryParse(x, out var id)) return "Invalid";
                    if(ctx.Guild.Members.ContainsKey(id)) return "User";
                    if(ctx.Guild.Roles.ContainsKey(id)) return "Role";
                    if(ctx.Guild.Channels.ContainsKey(id)) return "Channel";
                    var user = await ctx.Client.GetUserAsync(id);
                    if(user != null) return "User";
                    return "Invalid";
                })
                .ToDictionary(k => k.Key.Result, v => v.ToArray());
    }
}
